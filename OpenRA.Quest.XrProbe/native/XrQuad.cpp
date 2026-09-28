/*
 * Copyright (c) The OpenRA Developers and Contributors.
 * SPDX-License-Identifier: GPL-3.0-or-later
 *
 * Minimal compositor-quad experiment. A later host will replace the test
 * pattern with OpenRA's frame and forward controller actions to the game.
 */

#include <jni.h>
#include <EGL/egl.h>
#include <GLES3/gl3.h>
#include <android/log.h>

#include <openxr/openxr.h>
#include <openxr/openxr_platform.h>

#include "BoardGeometry.h"
#include "BeamGeometry.h"
#include "PointerTransitions.h"

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstdio>
#include <cstring>
#include <memory>
#include <mutex>
#include <thread>
#include <vector>

namespace {

constexpr char LogTag[] = "OpenRA.XrProbe";
using OpenRaXr::BoardHeight;
using OpenRaXr::BoardWidth;
using OpenRaXr::MapAimToBoard;
using OpenRaXr::Rotate;
std::atomic<jlong> nextSessionToken{0};
std::atomic<jlong> cancelledThrough{0};
std::atomic_bool active{false};
std::mutex submittedFrameMutex;
std::shared_ptr<const std::vector<uint8_t>> submittedFrame;
std::atomic_bool rayVisible{true};
std::atomic_int rayThickness{1};
std::atomic_int rayColor{0};
std::atomic_int targetStyle{1};
// Board layout from OpenRA's VR settings tab; a change re-places the board in front of the gaze.
std::atomic<float> boardDistance{1.4f};
std::atomic<float> boardWidthMeters{OpenRaXr::BoardWidthMeters};
std::atomic<float> boardHeightOffset{0.0f};
std::atomic_bool replaceBoard{false};
// Passthrough from OpenRA's VR settings: mode 0 off, 1 background, 2 background + see-through
// unexplored map (the C# side makes those pixels transparent); look 0 color, 1 grayscale, 2 dimmed.
std::atomic_int passthroughMode{0};
std::atomic<float> passthroughOpacity{1.0f};
std::atomic_int passthroughLook{0};
std::atomic_bool passthroughAvailable{false};

void PointerColor(float& r, float& g, float& b)
{
    switch (rayColor.load()) {
        case 1: r = 1.0f; g = 0.84f; b = 0.36f; break;
        case 2: r = 0.35f; g = 0.70f; b = 1.0f; break;
        case 3: r = 0.48f; g = 0.53f; b = 0.59f; break;
        default: r = 0.96f; g = 0.97f; b = 1.0f; break;
    }
}

jstring Failure(JNIEnv* env, const char* stage, XrResult result)
{
    char message[192];
    std::snprintf(message, sizeof(message), "%s: XrResult %d", stage, static_cast<int>(result));
    __android_log_print(ANDROID_LOG_ERROR, LogTag, "%s", message);
    return env->NewStringUTF(message);
}

jstring Failure(JNIEnv* env, const char* message)
{
    __android_log_print(ANDROID_LOG_ERROR, LogTag, "%s", message);
    return env->NewStringUTF(message);
}

struct Resources {
    XrInstance instance = XR_NULL_HANDLE;
    XrSession session = XR_NULL_HANDLE;
    XrSpace space = XR_NULL_HANDLE;
    XrSpace viewSpace = XR_NULL_HANDLE;
    XrSpace aimSpace = XR_NULL_HANDLE;
    XrSwapchain swapchain = XR_NULL_HANDLE;
    XrSwapchain beamSwapchain = XR_NULL_HANDLE;
    XrActionSet actionSet = XR_NULL_HANDLE;
    EGLDisplay display = EGL_NO_DISPLAY;
    EGLSurface surface = EGL_NO_SURFACE;
    EGLContext context = EGL_NO_CONTEXT;
    GLuint framebuffer = 0;

    // XR_FB_passthrough (optional): camera view of the room behind the board.
    bool passthroughSupported = false;
    bool passthroughRunning = false;
    XrPassthroughFB passthrough = XR_NULL_HANDLE;
    XrPassthroughLayerFB passthroughLayer = XR_NULL_HANDLE;
    PFN_xrCreatePassthroughFB createPassthrough = nullptr;
    PFN_xrDestroyPassthroughFB destroyPassthrough = nullptr;
    PFN_xrPassthroughStartFB startPassthrough = nullptr;
    PFN_xrPassthroughPauseFB pausePassthrough = nullptr;
    PFN_xrCreatePassthroughLayerFB createPassthroughLayer = nullptr;
    PFN_xrDestroyPassthroughLayerFB destroyPassthroughLayer = nullptr;
    PFN_xrPassthroughLayerResumeFB resumePassthroughLayer = nullptr;
    PFN_xrPassthroughLayerPauseFB pausePassthroughLayer = nullptr;
    PFN_xrPassthroughLayerSetStyleFB setPassthroughStyle = nullptr;
    int passthroughStyleApplied = -1;

    ~Resources()
    {
        // Passthrough objects belong to the session and must go before it.
        if (passthroughLayer != XR_NULL_HANDLE && destroyPassthroughLayer)
            destroyPassthroughLayer(passthroughLayer);
        if (passthrough != XR_NULL_HANDLE && destroyPassthrough)
            destroyPassthrough(passthrough);
        if (framebuffer != 0)
            glDeleteFramebuffers(1, &framebuffer);
        if (beamSwapchain != XR_NULL_HANDLE)
            xrDestroySwapchain(beamSwapchain);
        if (swapchain != XR_NULL_HANDLE)
            xrDestroySwapchain(swapchain);
        if (aimSpace != XR_NULL_HANDLE)
            xrDestroySpace(aimSpace);
        if (viewSpace != XR_NULL_HANDLE)
            xrDestroySpace(viewSpace);
        if (space != XR_NULL_HANDLE)
            xrDestroySpace(space);
        if (session != XR_NULL_HANDLE)
            xrDestroySession(session);
        if (actionSet != XR_NULL_HANDLE)
            xrDestroyActionSet(actionSet);
        if (instance != XR_NULL_HANDLE)
            xrDestroyInstance(instance);
        if (display != EGL_NO_DISPLAY) {
            eglMakeCurrent(display, EGL_NO_SURFACE, EGL_NO_SURFACE, EGL_NO_CONTEXT);
            if (surface != EGL_NO_SURFACE)
                eglDestroySurface(display, surface);
            if (context != EGL_NO_CONTEXT)
                eglDestroyContext(display, context);
            eglTerminate(display);
        }
    }
};

// Starts, pauses and styles passthrough to match the settings. Failure silently keeps black.
void UpdatePassthrough(Resources& resources)
{
    if (!resources.passthroughSupported || resources.session == XR_NULL_HANDLE)
        return;

    const int mode = passthroughMode.load();
    const float opacity = std::clamp(passthroughOpacity.load(), 0.0f, 1.0f);
    const bool wanted = mode > 0 && opacity > 0.0f;
    if (!wanted) {
        if (resources.passthroughRunning) {
            resources.pausePassthroughLayer(resources.passthroughLayer);
            resources.pausePassthrough(resources.passthrough);
            resources.passthroughRunning = false;
        }
        return;
    }

    if (!resources.passthroughRunning) {
        XrResult result = XR_SUCCESS;
        if (resources.passthrough == XR_NULL_HANDLE) {
            XrPassthroughCreateInfoFB info{XR_TYPE_PASSTHROUGH_CREATE_INFO_FB};
            result = resources.createPassthrough(resources.session, &info, &resources.passthrough);
        }
        if (XR_SUCCEEDED(result) && resources.passthroughLayer == XR_NULL_HANDLE) {
            XrPassthroughLayerCreateInfoFB info{XR_TYPE_PASSTHROUGH_LAYER_CREATE_INFO_FB};
            info.passthrough = resources.passthrough;
            info.purpose = XR_PASSTHROUGH_LAYER_PURPOSE_RECONSTRUCTION_FB;
            result = resources.createPassthroughLayer(resources.session, &info, &resources.passthroughLayer);
        }
        if (XR_SUCCEEDED(result))
            result = resources.startPassthrough(resources.passthrough);
        if (XR_SUCCEEDED(result))
            result = resources.resumePassthroughLayer(resources.passthroughLayer);
        resources.passthroughRunning = XR_SUCCEEDED(result);
        resources.passthroughStyleApplied = -1;
        __android_log_print(ANDROID_LOG_INFO, LogTag, "Passthrough-Start: %d", static_cast<int>(result));
        if (!resources.passthroughRunning) {
            // Do not retry every frame; a settings change sets the mode again.
            passthroughMode.store(0);
            return;
        }
    }

    const int look = std::clamp(passthroughLook.load(), 0, 2);
    const int styleKey = look * 1000 + static_cast<int>(opacity * 100.0f);
    if (styleKey == resources.passthroughStyleApplied)
        return;

    XrPassthroughBrightnessContrastSaturationFB adjust{XR_TYPE_PASSTHROUGH_BRIGHTNESS_CONTRAST_SATURATION_FB};
    adjust.brightness = look == 2 ? -35.0f : 0.0f;
    adjust.contrast = 1.0f;
    adjust.saturation = look == 1 ? 0.0f : look == 2 ? 0.7f : 1.0f;
    XrPassthroughStyleFB style{XR_TYPE_PASSTHROUGH_STYLE_FB};
    style.next = look != 0 ? &adjust : nullptr;
    style.textureOpacityFactor = opacity;
    style.edgeColor = {0.0f, 0.0f, 0.0f, 0.0f};
    if (XR_SUCCEEDED(resources.setPassthroughStyle(resources.passthroughLayer, &style)))
        resources.passthroughStyleApplied = styleKey;
}

bool Supports(const std::vector<XrExtensionProperties>& extensions, const char* name)
{
    return std::any_of(extensions.begin(), extensions.end(), [name](const auto& extension) {
        return std::strcmp(extension.extensionName, name) == 0;
    });
}

bool CreateEgl(Resources& resources, EGLConfig& config)
{
    resources.display = eglGetDisplay(EGL_DEFAULT_DISPLAY);
    if (resources.display == EGL_NO_DISPLAY || !eglInitialize(resources.display, nullptr, nullptr))
        return false;
    if (!eglBindAPI(EGL_OPENGL_ES_API))
        return false;

    const EGLint attributes[] = {
        EGL_RED_SIZE, 8, EGL_GREEN_SIZE, 8, EGL_BLUE_SIZE, 8, EGL_ALPHA_SIZE, 8,
        EGL_RENDERABLE_TYPE, EGL_OPENGL_ES3_BIT, EGL_SURFACE_TYPE, EGL_PBUFFER_BIT, EGL_NONE
    };
    EGLint count = 0;
    if (!eglChooseConfig(resources.display, attributes, &config, 1, &count) || count == 0)
        return false;

    const EGLint contextAttributes[] = {EGL_CONTEXT_CLIENT_VERSION, 3, EGL_NONE};
    resources.context = eglCreateContext(resources.display, config, EGL_NO_CONTEXT, contextAttributes);
    if (resources.context == EGL_NO_CONTEXT)
        return false;

    const EGLint surfaceAttributes[] = {EGL_WIDTH, 16, EGL_HEIGHT, 16, EGL_NONE};
    resources.surface = eglCreatePbufferSurface(resources.display, config, surfaceAttributes);
    return resources.surface != EGL_NO_SURFACE &&
        eglMakeCurrent(resources.display, resources.surface, resources.surface, resources.context);
}

// Game image on the XR thread's own context. Uploaded only when a new game frame arrives
// (~30 Hz), then copied on the GPU into each swapchain image (72-90 Hz) below the cursor.
struct BoardImage {
    GLuint texture = 0;
    GLuint readFramebuffer = 0;
    const std::vector<uint8_t>* uploaded = nullptr;
    std::shared_ptr<const std::vector<uint8_t>> keepAlive;
};
BoardImage boardImage;

bool UploadBoardImage(const std::shared_ptr<const std::vector<uint8_t>>& frame)
{
    if (boardImage.texture == 0) {
        glGenTextures(1, &boardImage.texture);
        glBindTexture(GL_TEXTURE_2D, boardImage.texture);
        glTexStorage2D(GL_TEXTURE_2D, 1, GL_RGBA8, BoardWidth, BoardHeight);
        glGenFramebuffers(1, &boardImage.readFramebuffer);
    }

    if (frame.get() != boardImage.uploaded) {
        glBindTexture(GL_TEXTURE_2D, boardImage.texture);
        glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
        glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, BoardWidth, BoardHeight,
            GL_RGBA, GL_UNSIGNED_BYTE, frame->data());
        glBindTexture(GL_TEXTURE_2D, 0);
        boardImage.uploaded = frame.get();
        boardImage.keepAlive = frame;
    }

    glBindFramebuffer(GL_READ_FRAMEBUFFER, boardImage.readFramebuffer);
    glFramebufferTexture2D(GL_READ_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, boardImage.texture, 0);
    return glCheckFramebufferStatus(GL_READ_FRAMEBUFFER) == GL_FRAMEBUFFER_COMPLETE;
}

bool DrawBoard(GLuint framebuffer, GLuint texture, int cursorX, int cursorY,
    bool pressed, bool contextPressed)
{
    std::shared_ptr<const std::vector<uint8_t>> frame;
    {
        const std::lock_guard<std::mutex> lock(submittedFrameMutex);
        frame = submittedFrame;
    }

    glBindFramebuffer(GL_FRAMEBUFFER, framebuffer);
    glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, texture, 0);
    if (glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
        return false;

    glViewport(0, 0, BoardWidth, BoardHeight);
    glDisable(GL_SCISSOR_TEST);
    if (frame && UploadBoardImage(frame)) {
        glBlitFramebuffer(0, 0, BoardWidth, BoardHeight, 0, 0, BoardWidth, BoardHeight,
            GL_COLOR_BUFFER_BIT, GL_NEAREST);
        glBindFramebuffer(GL_READ_FRAMEBUFFER, 0);
        glBindFramebuffer(GL_FRAMEBUFFER, framebuffer);
    } else {
        glClearColor(0.07f, 0.10f, 0.18f, 1.0f);
        glClear(GL_COLOR_BUFFER_BIT);
        glEnable(GL_SCISSOR_TEST);
        glScissor(48, 48, BoardWidth - 96, BoardHeight - 96);
        glClearColor(0.12f, 0.37f, 0.25f, 1.0f);
        glClear(GL_COLOR_BUFFER_BIT);
        glScissor(BoardWidth / 2 - 8, 48, 16, BoardHeight - 96);
        glClearColor(0.78f, 0.72f, 0.44f, 1.0f);
        glClear(GL_COLOR_BUFFER_BIT);
    }
    glEnable(GL_SCISSOR_TEST);
    if (cursorX >= 0 && cursorY >= 0) {
        float red, green, blue;
        PointerColor(red, green, blue);
        if (pressed) { red = 1.0f; green = 0.28f; blue = 0.22f; }
        else if (contextPressed) { red = 0.35f; green = 0.65f; blue = 1.0f; }
        glClearColor(red, green, blue, 1.0f);
        const int radius = 8 + 4 * std::clamp(rayThickness.load(), 0, 2);
        const int stroke = 2 + std::clamp(rayThickness.load(), 0, 2);
        auto fill = [&](int x, int y, int width, int height) {
            glScissor(std::max(0, x), std::max(0, y), width, height);
            glClear(GL_COLOR_BUFFER_BIT);
        };
        switch (targetStyle.load()) {
            case 0:
                fill(cursorX - stroke, cursorY - stroke, 2 * stroke + 1, 2 * stroke + 1);
                break;
            case 2:
                fill(cursorX - radius, cursorY - stroke / 2, 2 * radius + 1, stroke);
                fill(cursorX - stroke / 2, cursorY - radius, stroke, 2 * radius + 1);
                break;
            default:
                fill(cursorX - radius, cursorY - radius, 2 * radius + 1, stroke);
                fill(cursorX - radius, cursorY + radius - stroke, 2 * radius + 1, stroke);
                fill(cursorX - radius, cursorY - radius, stroke, 2 * radius + 1);
                fill(cursorX + radius - stroke, cursorY - radius, stroke, 2 * radius + 1);
                break;
        }
    }
    glDisable(GL_SCISSOR_TEST);
    glBindFramebuffer(GL_FRAMEBUFFER, 0);
    glFlush();
    return glGetError() == GL_NO_ERROR;
}

bool DrawRayTexture(GLuint framebuffer, GLuint texture)
{
    glBindFramebuffer(GL_FRAMEBUFFER, framebuffer);
    glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, texture, 0);
    if (glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
        return false;
    glViewport(0, 0, 8, 8);
    glDisable(GL_SCISSOR_TEST);
    float red, green, blue;
    PointerColor(red, green, blue);
    glClearColor(red, green, blue, 0.82f);
    glClear(GL_COLOR_BUFFER_BIT);
    glBindFramebuffer(GL_FRAMEBUFFER, 0);
    glFlush();
    return glGetError() == GL_NO_ERROR;
}

struct ActiveGuard {
    ~ActiveGuard() { active.store(false); }
};

struct PointerDispatcher {
    JNIEnv* env;
    jclass probeClass;
    jmethodID callback;
    OpenRaXr::PointerTransitions pointer;

    void Emit(OpenRaXr::PointerEvent event)
    {
        env->CallStaticVoidMethod(probeClass, callback, static_cast<jint>(event.type),
            static_cast<jint>(event.x), static_cast<jint>(event.y));
        if (env->ExceptionCheck()) {
            env->ExceptionDescribe();
            env->ExceptionClear();
        }
    }

    void Update(int x, int y, bool pressed, bool contextPressed, bool panPressed, bool additive)
    {
        pointer.Update(x >= 0 && y >= 0, x, y, pressed, contextPressed, panPressed, additive,
            [this](OpenRaXr::PointerEvent event) { Emit(event); });
    }

    ~PointerDispatcher()
    {
        pointer.Release([this](OpenRaXr::PointerEvent event) { Emit(event); });
    }
};

} // namespace

extern "C" JNIEXPORT void JNICALL
Java_com_friedensbringer_openra_xr_XrProbe_setPointerStyle(JNIEnv*, jclass,
    jboolean visible, jint thickness, jint color, jint target)
{
    rayVisible.store(visible == JNI_TRUE);
    rayThickness.store(std::clamp(static_cast<int>(thickness), 0, 2));
    rayColor.store(std::clamp(static_cast<int>(color), 0, 3));
    targetStyle.store(std::clamp(static_cast<int>(target), 0, 2));
}

extern "C" JNIEXPORT void JNICALL
Java_com_friedensbringer_openra_xr_XrProbe_setBoardLayout(JNIEnv*, jclass,
    jfloat distance, jfloat width, jfloat heightOffset)
{
    const float newDistance = std::clamp(static_cast<float>(distance), 0.8f, 3.0f);
    const float newWidth = std::clamp(static_cast<float>(width), 1.0f, 3.2f);
    const float newOffset = std::clamp(static_cast<float>(heightOffset), -0.5f, 0.5f);
    if (newDistance == boardDistance.load() && newWidth == boardWidthMeters.load() &&
        newOffset == boardHeightOffset.load())
        return;

    boardDistance.store(newDistance);
    boardWidthMeters.store(newWidth);
    boardHeightOffset.store(newOffset);
    replaceBoard.store(true);
}

extern "C" JNIEXPORT void JNICALL
Java_com_friedensbringer_openra_xr_XrProbe_requestRecenter(JNIEnv*, jclass)
{
    replaceBoard.store(true);
}

extern "C" JNIEXPORT void JNICALL
Java_com_friedensbringer_openra_xr_XrProbe_setPassthrough(JNIEnv*, jclass,
    jint mode, jfloat opacity, jint look)
{
    passthroughMode.store(std::clamp(static_cast<int>(mode), 0, 2));
    passthroughOpacity.store(std::clamp(static_cast<float>(opacity), 0.0f, 1.0f));
    passthroughLook.store(std::clamp(static_cast<int>(look), 0, 2));
}

extern "C" JNIEXPORT jboolean JNICALL
Java_com_friedensbringer_openra_xr_XrProbe_isPassthroughAvailable(JNIEnv*, jclass)
{
    return passthroughAvailable.load() ? JNI_TRUE : JNI_FALSE;
}

extern "C" JNIEXPORT jlong JNICALL
Java_com_friedensbringer_openra_xr_XrProbe_beginSession(JNIEnv*, jclass)
{
    return nextSessionToken.fetch_add(1) + 1;
}

extern "C" JNIEXPORT void JNICALL
Java_com_friedensbringer_openra_xr_XrProbe_requestStop(JNIEnv*, jclass, jlong token)
{
    auto previous = cancelledThrough.load();
    while (previous < token && !cancelledThrough.compare_exchange_weak(previous, token)) {}
}

extern "C" JNIEXPORT jboolean JNICALL
Java_com_friedensbringer_openra_xr_XrProbe_submitFrame(JNIEnv* env, jclass, jbyteArray rgba)
{
    constexpr jsize expectedSize = BoardWidth * BoardHeight * 4;
    if (rgba == nullptr || env->GetArrayLength(rgba) != expectedSize)
        return JNI_FALSE;

    auto frame = std::make_shared<std::vector<uint8_t>>(expectedSize);
    env->GetByteArrayRegion(rgba, 0, expectedSize, reinterpret_cast<jbyte*>(frame->data()));
    if (env->ExceptionCheck())
        return JNI_FALSE;

    {
        const std::lock_guard<std::mutex> lock(submittedFrameMutex);
        submittedFrame = std::move(frame);
    }
    return JNI_TRUE;
}

namespace {

// Asynchronous readback of OpenRA's finished screen texture on the game's GL thread.
// Two pixel-pack buffers alternate: this frame's glReadPixels is queued while the previous
// one is mapped and converted, so the GPU never stalls. Costs one frame (~33 ms) of latency.
struct FrameCapture {
    GLuint framebuffer = 0;
    GLuint buffers[2] = {0, 0};
    bool filled[2] = {false, false};
    std::vector<uint8_t> masks[2];
    int maskBlocksX[2] = {0, 0};
    int next = 0;
};
FrameCapture capture;

// Shroud is pure black; a small tolerance keeps soft edges and dark terrain opaque.
constexpr uint8_t SeeThroughMaxChannel = 10;

std::shared_ptr<const std::vector<uint8_t>> ConvertCapturedFrame(const uint8_t* rgba,
    const std::vector<uint8_t>& mask, int blocksX, int blockSize)
{
    auto frame = std::make_shared<std::vector<uint8_t>>(BoardWidth * BoardHeight * 4);
    const bool useMask = blocksX > 0 && blockSize > 0 &&
        blocksX >= (BoardWidth + blockSize - 1) / blockSize &&
        mask.size() >= static_cast<size_t>(blocksX) * ((BoardHeight + blockSize - 1) / blockSize);
    for (int y = 0; y < BoardHeight; ++y) {
        // Readback row r is screen row r (top first); board row 0 is the bottom of the quad.
        const int screenRow = BoardHeight - 1 - y;
        const uint8_t* source = rgba + static_cast<size_t>(screenRow) * BoardWidth * 4;
        uint8_t* target = frame->data() + static_cast<size_t>(y) * BoardWidth * 4;
        const uint8_t* maskRow = useMask ? mask.data() + (screenRow / blockSize) * blocksX : nullptr;
        for (int x = 0; x < BoardWidth; ++x, source += 4, target += 4) {
            if (maskRow && maskRow[x / blockSize] && source[0] <= SeeThroughMaxChannel &&
                source[1] <= SeeThroughMaxChannel && source[2] <= SeeThroughMaxChannel) {
                target[0] = target[1] = target[2] = target[3] = 0;
                continue;
            }

            target[0] = source[0];
            target[1] = source[1];
            target[2] = source[2];
            target[3] = 255;
        }
    }

    return frame;
}

} // namespace

// Called on OpenRA's GL thread right after a frame was rendered. Returns true when a frame
// (the previous one) was converted and handed to the XR thread.
extern "C" JNIEXPORT jboolean JNICALL
Java_com_friedensbringer_openra_xr_XrProbe_captureFrame(JNIEnv* env, jclass,
    jint texture, jint width, jint height, jbyteArray mask, jint blocksX, jint blockSize)
{
    if (width != BoardWidth || height != BoardHeight || texture <= 0)
        return JNI_FALSE;

    GLint previousDrawFramebuffer = 0;
    GLint previousReadFramebuffer = 0;
    GLint previousPackBuffer = 0;
    glGetIntegerv(GL_DRAW_FRAMEBUFFER_BINDING, &previousDrawFramebuffer);
    glGetIntegerv(GL_READ_FRAMEBUFFER_BINDING, &previousReadFramebuffer);
    glGetIntegerv(GL_PIXEL_PACK_BUFFER_BINDING, &previousPackBuffer);

    constexpr GLsizeiptr frameBytes = BoardWidth * BoardHeight * 4;
    if (capture.framebuffer == 0) {
        glGenFramebuffers(1, &capture.framebuffer);
        glGenBuffers(2, capture.buffers);
        for (GLuint buffer : capture.buffers) {
            glBindBuffer(GL_PIXEL_PACK_BUFFER, buffer);
            glBufferData(GL_PIXEL_PACK_BUFFER, frameBytes, nullptr, GL_STREAM_READ);
        }
    }

    bool published = false;
    glBindFramebuffer(GL_FRAMEBUFFER, capture.framebuffer);
    glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, static_cast<GLuint>(texture), 0);
    if (glCheckFramebufferStatus(GL_FRAMEBUFFER) == GL_FRAMEBUFFER_COMPLETE) {
        const int slot = capture.next;
        glBindBuffer(GL_PIXEL_PACK_BUFFER, capture.buffers[slot]);
        glPixelStorei(GL_PACK_ALIGNMENT, 1);
        glReadPixels(0, 0, BoardWidth, BoardHeight, GL_RGBA, GL_UNSIGNED_BYTE, nullptr);
        capture.filled[slot] = true;
        capture.maskBlocksX[slot] = blocksX;
        if (mask != nullptr) {
            capture.masks[slot].resize(static_cast<size_t>(env->GetArrayLength(mask)));
            env->GetByteArrayRegion(mask, 0, static_cast<jsize>(capture.masks[slot].size()),
                reinterpret_cast<jbyte*>(capture.masks[slot].data()));
        } else
            capture.masks[slot].clear();

        const int ready = slot ^ 1;
        if (capture.filled[ready]) {
            glBindBuffer(GL_PIXEL_PACK_BUFFER, capture.buffers[ready]);
            const auto* pixels = static_cast<const uint8_t*>(
                glMapBufferRange(GL_PIXEL_PACK_BUFFER, 0, frameBytes, GL_MAP_READ_BIT));
            if (pixels != nullptr) {
                auto frame = ConvertCapturedFrame(pixels, capture.masks[ready],
                    capture.maskBlocksX[ready], blockSize);
                glUnmapBuffer(GL_PIXEL_PACK_BUFFER);
                const std::lock_guard<std::mutex> lock(submittedFrameMutex);
                submittedFrame = std::move(frame);
                published = true;
            }
        }

        capture.next = ready;
    }

    glBindBuffer(GL_PIXEL_PACK_BUFFER, static_cast<GLuint>(previousPackBuffer));
    glBindFramebuffer(GL_DRAW_FRAMEBUFFER, static_cast<GLuint>(previousDrawFramebuffer));
    glBindFramebuffer(GL_READ_FRAMEBUFFER, static_cast<GLuint>(previousReadFramebuffer));
    return published ? JNI_TRUE : JNI_FALSE;
}

// The game's GL context was recreated: its buffers are gone, start over.
extern "C" JNIEXPORT void JNICALL
Java_com_friedensbringer_openra_xr_XrProbe_resetCapture(JNIEnv*, jclass)
{
    capture = {};
}

extern "C" JNIEXPORT jstring JNICALL
Java_com_friedensbringer_openra_xr_XrProbe_showQuad(JNIEnv* env, jclass probeClass, jobject activity, jlong token)
{
    if (token <= 0 || token > nextSessionToken.load() || token <= cancelledThrough.load())
        return Failure(env, "OpenXR-Start abgebrochen oder ungültig");

    const auto startDeadline = std::chrono::steady_clock::now() + std::chrono::seconds(7);
    bool expected = false;
    while (!active.compare_exchange_weak(expected, true)) {
        if (token <= cancelledThrough.load())
            return Failure(env, "OpenXR-Start abgebrochen");
        if (std::chrono::steady_clock::now() >= startDeadline)
            return Failure(env, "Vorige OpenXR-Session wird noch beendet");
        expected = false;
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }

    ActiveGuard guard;
    if (token <= cancelledThrough.load())
        return Failure(env, "OpenXR-Start abgebrochen");
    if (activity == nullptr)
        return Failure(env, "Android Activity fehlt");

    JavaVM* vm = nullptr;
    if (env->GetJavaVM(&vm) != JNI_OK || vm == nullptr)
        return Failure(env, "JavaVM nicht verfügbar");

    const jmethodID pointerCallback = env->GetStaticMethodID(probeClass, "onPointerEvent", "(III)V");
    if (pointerCallback == nullptr) {
        env->ExceptionClear();
        return Failure(env, "Java-Callback für XR-Zeiger fehlt");
    }

    PFN_xrInitializeLoaderKHR initializeLoader = nullptr;
    XrResult result = xrGetInstanceProcAddr(XR_NULL_HANDLE, "xrInitializeLoaderKHR",
        reinterpret_cast<PFN_xrVoidFunction*>(&initializeLoader));
    if (XR_FAILED(result) || initializeLoader == nullptr)
        return Failure(env, "xrGetInstanceProcAddr(xrInitializeLoaderKHR)", result);

    XrLoaderInitInfoAndroidKHR loaderInfo{XR_TYPE_LOADER_INIT_INFO_ANDROID_KHR};
    loaderInfo.applicationVM = vm;
    loaderInfo.applicationContext = activity;
    result = initializeLoader(reinterpret_cast<const XrLoaderInitInfoBaseHeaderKHR*>(&loaderInfo));
    if (XR_FAILED(result))
        return Failure(env, "xrInitializeLoaderKHR", result);

    uint32_t extensionCount = 0;
    result = xrEnumerateInstanceExtensionProperties(nullptr, 0, &extensionCount, nullptr);
    if (XR_FAILED(result))
        return Failure(env, "xrEnumerateInstanceExtensionProperties", result);

    std::vector<XrExtensionProperties> extensions(extensionCount);
    for (auto& extension : extensions)
        extension.type = XR_TYPE_EXTENSION_PROPERTIES;
    result = xrEnumerateInstanceExtensionProperties(nullptr, extensionCount, &extensionCount, extensions.data());
    if (XR_FAILED(result))
        return Failure(env, "xrEnumerateInstanceExtensionProperties(list)", result);

    std::vector<const char*> enabledExtensions = {
        XR_KHR_ANDROID_CREATE_INSTANCE_EXTENSION_NAME,
        XR_KHR_OPENGL_ES_ENABLE_EXTENSION_NAME,
    };
    for (const char* extension : enabledExtensions)
        if (!Supports(extensions, extension))
            return Failure(env, extension);

    Resources resources;
    resources.passthroughSupported = Supports(extensions, XR_FB_PASSTHROUGH_EXTENSION_NAME);
    if (resources.passthroughSupported)
        enabledExtensions.push_back(XR_FB_PASSTHROUGH_EXTENSION_NAME);
    PointerDispatcher pointer{env, probeClass, pointerCallback};
    XrInstanceCreateInfoAndroidKHR androidInfo{XR_TYPE_INSTANCE_CREATE_INFO_ANDROID_KHR};
    androidInfo.applicationVM = vm;
    androidInfo.applicationActivity = activity;
    XrInstanceCreateInfo instanceInfo{XR_TYPE_INSTANCE_CREATE_INFO};
    instanceInfo.next = &androidInfo;
    std::snprintf(instanceInfo.applicationInfo.applicationName,
        sizeof(instanceInfo.applicationInfo.applicationName), "OpenRA XR Quad Probe");
    std::snprintf(instanceInfo.applicationInfo.engineName,
        sizeof(instanceInfo.applicationInfo.engineName), "OpenRA");
    instanceInfo.applicationInfo.applicationVersion = 1;
    instanceInfo.applicationInfo.engineVersion = 1;
    instanceInfo.applicationInfo.apiVersion = XR_API_VERSION_1_0;
    instanceInfo.enabledExtensionCount = static_cast<uint32_t>(enabledExtensions.size());
    instanceInfo.enabledExtensionNames = enabledExtensions.data();
    result = xrCreateInstance(&instanceInfo, &resources.instance);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateInstance", result);

    XrSystemGetInfo systemInfo{XR_TYPE_SYSTEM_GET_INFO};
    systemInfo.formFactor = XR_FORM_FACTOR_HEAD_MOUNTED_DISPLAY;
    XrSystemId systemId = XR_NULL_SYSTEM_ID;
    result = xrGetSystem(resources.instance, &systemInfo, &systemId);
    if (XR_FAILED(result))
        return Failure(env, "xrGetSystem", result);
    XrSystemProperties systemProperties{XR_TYPE_SYSTEM_PROPERTIES};
    XrSystemPassthroughPropertiesFB passthroughProperties{XR_TYPE_SYSTEM_PASSTHROUGH_PROPERTIES_FB};
    if (resources.passthroughSupported)
        systemProperties.next = &passthroughProperties;
    result = xrGetSystemProperties(resources.instance, systemId, &systemProperties);
    if (XR_FAILED(result))
        return Failure(env, "xrGetSystemProperties", result);
    resources.passthroughSupported = resources.passthroughSupported && passthroughProperties.supportsPassthrough;
    if (resources.passthroughSupported) {
        auto load = [&](const char* name, auto& function) {
            return XR_SUCCEEDED(xrGetInstanceProcAddr(resources.instance, name,
                reinterpret_cast<PFN_xrVoidFunction*>(&function))) && function != nullptr;
        };
        resources.passthroughSupported = load("xrCreatePassthroughFB", resources.createPassthrough) &&
            load("xrDestroyPassthroughFB", resources.destroyPassthrough) &&
            load("xrPassthroughStartFB", resources.startPassthrough) &&
            load("xrPassthroughPauseFB", resources.pausePassthrough) &&
            load("xrCreatePassthroughLayerFB", resources.createPassthroughLayer) &&
            load("xrDestroyPassthroughLayerFB", resources.destroyPassthroughLayer) &&
            load("xrPassthroughLayerResumeFB", resources.resumePassthroughLayer) &&
            load("xrPassthroughLayerPauseFB", resources.pausePassthroughLayer) &&
            load("xrPassthroughLayerSetStyleFB", resources.setPassthroughStyle);
    }
    passthroughAvailable.store(resources.passthroughSupported);
    __android_log_print(ANDROID_LOG_INFO, LogTag, "Passthrough verfügbar: %d",
        resources.passthroughSupported ? 1 : 0);

    XrPath rightHandPath = XR_NULL_PATH;
    XrPath leftHandPath = XR_NULL_PATH;
    XrPath touchProfile = XR_NULL_PATH;
    XrPath aimBinding = XR_NULL_PATH;
    XrPath triggerBinding = XR_NULL_PATH;
    XrPath contextBinding = XR_NULL_PATH;
    XrPath panBinding = XR_NULL_PATH;
    XrPath additiveBinding = XR_NULL_PATH;
    XrPath zoomBinding = XR_NULL_PATH;
    XrPath menuBinding = XR_NULL_PATH;
    XrPath deployBinding = XR_NULL_PATH;
    result = xrStringToPath(resources.instance, "/user/hand/right", &rightHandPath);
    if (XR_FAILED(result))
        return Failure(env, "xrStringToPath(right hand)", result);
    result = xrStringToPath(resources.instance, "/user/hand/left", &leftHandPath);
    if (XR_FAILED(result))
        return Failure(env, "xrStringToPath(left hand)", result);
    result = xrStringToPath(resources.instance, "/interaction_profiles/oculus/touch_controller", &touchProfile);
    if (XR_FAILED(result))
        return Failure(env, "xrStringToPath(Touch profile)", result);
    result = xrStringToPath(resources.instance, "/user/hand/right/input/aim/pose", &aimBinding);
    if (XR_FAILED(result))
        return Failure(env, "xrStringToPath(aim pose)", result);
    result = xrStringToPath(resources.instance, "/user/hand/right/input/trigger/value", &triggerBinding);
    if (XR_FAILED(result))
        return Failure(env, "xrStringToPath(trigger)", result);
    result = xrStringToPath(resources.instance, "/user/hand/right/input/a/click", &contextBinding);
    if (XR_FAILED(result))
        return Failure(env, "xrStringToPath(A button)", result);
    result = xrStringToPath(resources.instance, "/user/hand/right/input/squeeze/value", &panBinding);
    if (XR_FAILED(result))
        return Failure(env, "xrStringToPath(squeeze)", result);
    result = xrStringToPath(resources.instance, "/user/hand/right/input/b/click", &additiveBinding);
    if (XR_FAILED(result))
        return Failure(env, "xrStringToPath(B button)", result);
    result = xrStringToPath(resources.instance, "/user/hand/right/input/thumbstick/y", &zoomBinding);
    if (XR_FAILED(result))
        return Failure(env, "xrStringToPath(thumbstick y)", result);
    result = xrStringToPath(resources.instance, "/user/hand/left/input/menu/click", &menuBinding);
    if (XR_FAILED(result))
        return Failure(env, "xrStringToPath(left menu)", result);
    result = xrStringToPath(resources.instance, "/user/hand/left/input/x/click", &deployBinding);
    if (XR_FAILED(result))
        return Failure(env, "xrStringToPath(left X)", result);

    XrActionSetCreateInfo actionSetInfo{XR_TYPE_ACTION_SET_CREATE_INFO};
    std::snprintf(actionSetInfo.actionSetName, sizeof(actionSetInfo.actionSetName), "tabletop_probe");
    std::snprintf(actionSetInfo.localizedActionSetName,
        sizeof(actionSetInfo.localizedActionSetName), "Tabletop Probe");
    result = xrCreateActionSet(resources.instance, &actionSetInfo, &resources.actionSet);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateActionSet", result);

    XrAction aimAction = XR_NULL_HANDLE;
    XrAction triggerAction = XR_NULL_HANDLE;
    XrAction contextAction = XR_NULL_HANDLE;
    XrAction panAction = XR_NULL_HANDLE;
    XrAction additiveAction = XR_NULL_HANDLE;
    XrAction zoomAction = XR_NULL_HANDLE;
    XrAction menuAction = XR_NULL_HANDLE;
    XrAction deployAction = XR_NULL_HANDLE;
    XrActionCreateInfo actionInfo{XR_TYPE_ACTION_CREATE_INFO};
    actionInfo.actionType = XR_ACTION_TYPE_POSE_INPUT;
    actionInfo.countSubactionPaths = 1;
    actionInfo.subactionPaths = &rightHandPath;
    std::snprintf(actionInfo.actionName, sizeof(actionInfo.actionName), "aim_pose");
    std::snprintf(actionInfo.localizedActionName, sizeof(actionInfo.localizedActionName), "Aim at board");
    result = xrCreateAction(resources.actionSet, &actionInfo, &aimAction);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateAction(aim)", result);

    actionInfo = {XR_TYPE_ACTION_CREATE_INFO};
    actionInfo.actionType = XR_ACTION_TYPE_FLOAT_INPUT;
    actionInfo.countSubactionPaths = 1;
    actionInfo.subactionPaths = &rightHandPath;
    std::snprintf(actionInfo.actionName, sizeof(actionInfo.actionName), "select_trigger");
    std::snprintf(actionInfo.localizedActionName, sizeof(actionInfo.localizedActionName), "Select on board");
    result = xrCreateAction(resources.actionSet, &actionInfo, &triggerAction);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateAction(trigger)", result);

    actionInfo = {XR_TYPE_ACTION_CREATE_INFO};
    actionInfo.actionType = XR_ACTION_TYPE_BOOLEAN_INPUT;
    actionInfo.countSubactionPaths = 1;
    actionInfo.subactionPaths = &rightHandPath;
    std::snprintf(actionInfo.actionName, sizeof(actionInfo.actionName), "context_button");
    std::snprintf(actionInfo.localizedActionName, sizeof(actionInfo.localizedActionName), "Context command");
    result = xrCreateAction(resources.actionSet, &actionInfo, &contextAction);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateAction(context)", result);

    actionInfo = {XR_TYPE_ACTION_CREATE_INFO};
    actionInfo.actionType = XR_ACTION_TYPE_FLOAT_INPUT;
    actionInfo.countSubactionPaths = 1;
    actionInfo.subactionPaths = &rightHandPath;
    std::snprintf(actionInfo.actionName, sizeof(actionInfo.actionName), "pan_squeeze");
    std::snprintf(actionInfo.localizedActionName, sizeof(actionInfo.localizedActionName), "Drag the map");
    result = xrCreateAction(resources.actionSet, &actionInfo, &panAction);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateAction(pan)", result);

    actionInfo = {XR_TYPE_ACTION_CREATE_INFO};
    actionInfo.actionType = XR_ACTION_TYPE_BOOLEAN_INPUT;
    actionInfo.countSubactionPaths = 1;
    actionInfo.subactionPaths = &rightHandPath;
    std::snprintf(actionInfo.actionName, sizeof(actionInfo.actionName), "add_selection");
    std::snprintf(actionInfo.localizedActionName, sizeof(actionInfo.localizedActionName), "Add to selection");
    result = xrCreateAction(resources.actionSet, &actionInfo, &additiveAction);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateAction(add selection)", result);

    actionInfo = {XR_TYPE_ACTION_CREATE_INFO};
    actionInfo.actionType = XR_ACTION_TYPE_FLOAT_INPUT;
    actionInfo.countSubactionPaths = 1;
    actionInfo.subactionPaths = &rightHandPath;
    std::snprintf(actionInfo.actionName, sizeof(actionInfo.actionName), "zoom_stick");
    std::snprintf(actionInfo.localizedActionName, sizeof(actionInfo.localizedActionName), "Zoom the map");
    result = xrCreateAction(resources.actionSet, &actionInfo, &zoomAction);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateAction(zoom)", result);

    actionInfo = {XR_TYPE_ACTION_CREATE_INFO};
    actionInfo.actionType = XR_ACTION_TYPE_BOOLEAN_INPUT;
    actionInfo.countSubactionPaths = 1;
    actionInfo.subactionPaths = &leftHandPath;
    std::snprintf(actionInfo.actionName, sizeof(actionInfo.actionName), "quick_menu");
    std::snprintf(actionInfo.localizedActionName, sizeof(actionInfo.localizedActionName), "Open quick menu");
    result = xrCreateAction(resources.actionSet, &actionInfo, &menuAction);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateAction(menu)", result);

    actionInfo = {XR_TYPE_ACTION_CREATE_INFO};
    actionInfo.actionType = XR_ACTION_TYPE_BOOLEAN_INPUT;
    actionInfo.countSubactionPaths = 1;
    actionInfo.subactionPaths = &leftHandPath;
    std::snprintf(actionInfo.actionName, sizeof(actionInfo.actionName), "deploy_selected");
    std::snprintf(actionInfo.localizedActionName, sizeof(actionInfo.localizedActionName), "Deploy selected units");
    result = xrCreateAction(resources.actionSet, &actionInfo, &deployAction);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateAction(deploy)", result);

    const XrActionSuggestedBinding bindings[] = {
        {aimAction, aimBinding},
        {triggerAction, triggerBinding},
        {contextAction, contextBinding},
        {panAction, panBinding},
        {additiveAction, additiveBinding},
        {zoomAction, zoomBinding},
        {menuAction, menuBinding},
        {deployAction, deployBinding},
    };
    XrInteractionProfileSuggestedBinding profileBindings{XR_TYPE_INTERACTION_PROFILE_SUGGESTED_BINDING};
    profileBindings.interactionProfile = touchProfile;
    profileBindings.countSuggestedBindings = static_cast<uint32_t>(std::size(bindings));
    profileBindings.suggestedBindings = bindings;
    result = xrSuggestInteractionProfileBindings(resources.instance, &profileBindings);
    if (XR_FAILED(result))
        return Failure(env, "xrSuggestInteractionProfileBindings", result);

    PFN_xrGetOpenGLESGraphicsRequirementsKHR getGraphicsRequirements = nullptr;
    result = xrGetInstanceProcAddr(resources.instance, "xrGetOpenGLESGraphicsRequirementsKHR",
        reinterpret_cast<PFN_xrVoidFunction*>(&getGraphicsRequirements));
    if (XR_FAILED(result) || getGraphicsRequirements == nullptr)
        return Failure(env, "xrGetOpenGLESGraphicsRequirementsKHR", result);

    XrGraphicsRequirementsOpenGLESKHR requirements{XR_TYPE_GRAPHICS_REQUIREMENTS_OPENGL_ES_KHR};
    result = getGraphicsRequirements(resources.instance, systemId, &requirements);
    if (XR_FAILED(result))
        return Failure(env, "OpenGL-ES-Grafikanforderungen", result);
    EGLConfig config = nullptr;
    if (!CreateEgl(resources, config))
        return Failure(env, "EGL-3-Kontext konnte nicht erstellt werden");
    // GL names of a previous session died with its context.
    boardImage = {};
    GLint major = 0;
    GLint minor = 0;
    glGetIntegerv(GL_MAJOR_VERSION, &major);
    glGetIntegerv(GL_MINOR_VERSION, &minor);
    const XrVersion graphicsVersion = XR_MAKE_VERSION(major, minor, 0);
    if (graphicsVersion < requirements.minApiVersionSupported ||
        graphicsVersion > requirements.maxApiVersionSupported)
        return Failure(env, "EGL-Kontext liegt außerhalb der OpenXR-Grafikanforderungen");

    XrGraphicsBindingOpenGLESAndroidKHR graphicsBinding{XR_TYPE_GRAPHICS_BINDING_OPENGL_ES_ANDROID_KHR};
    graphicsBinding.display = resources.display;
    graphicsBinding.config = config;
    graphicsBinding.context = resources.context;
    XrSessionCreateInfo sessionInfo{XR_TYPE_SESSION_CREATE_INFO};
    sessionInfo.next = &graphicsBinding;
    sessionInfo.systemId = systemId;
    result = xrCreateSession(resources.instance, &sessionInfo, &resources.session);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateSession", result);

    XrActionSpaceCreateInfo aimSpaceInfo{XR_TYPE_ACTION_SPACE_CREATE_INFO};
    aimSpaceInfo.action = aimAction;
    aimSpaceInfo.subactionPath = rightHandPath;
    aimSpaceInfo.poseInActionSpace.orientation.w = 1.0f;
    result = xrCreateActionSpace(resources.session, &aimSpaceInfo, &resources.aimSpace);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateActionSpace(aim)", result);

    XrSessionActionSetsAttachInfo attachInfo{XR_TYPE_SESSION_ACTION_SETS_ATTACH_INFO};
    attachInfo.countActionSets = 1;
    attachInfo.actionSets = &resources.actionSet;
    result = xrAttachSessionActionSets(resources.session, &attachInfo);
    if (XR_FAILED(result))
        return Failure(env, "xrAttachSessionActionSets", result);

    XrReferenceSpaceCreateInfo spaceInfo{XR_TYPE_REFERENCE_SPACE_CREATE_INFO};
    spaceInfo.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_LOCAL;
    spaceInfo.poseInReferenceSpace.orientation.w = 1.0f;
    result = xrCreateReferenceSpace(resources.session, &spaceInfo, &resources.space);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateReferenceSpace(LOCAL)", result);

    spaceInfo.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_VIEW;
    result = xrCreateReferenceSpace(resources.session, &spaceInfo, &resources.viewSpace);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateReferenceSpace(VIEW)", result);

    uint32_t formatCount = 0;
    result = xrEnumerateSwapchainFormats(resources.session, 0, &formatCount, nullptr);
    if (XR_FAILED(result))
        return Failure(env, "xrEnumerateSwapchainFormats", result);
    std::vector<int64_t> formats(formatCount);
    result = xrEnumerateSwapchainFormats(resources.session, formatCount, &formatCount, formats.data());
    if (XR_FAILED(result))
        return Failure(env, "xrEnumerateSwapchainFormats(list)", result);

    auto found = std::find(formats.begin(), formats.end(), static_cast<int64_t>(GL_RGBA8));
    if (found == formats.end())
        found = std::find(formats.begin(), formats.end(), static_cast<int64_t>(GL_SRGB8_ALPHA8));
    if (found == formats.end())
        return Failure(env, "RGBA8- oder sRGB8-Swapchainformat fehlt");

    XrSwapchainCreateInfo swapchainInfo{XR_TYPE_SWAPCHAIN_CREATE_INFO};
    swapchainInfo.usageFlags = XR_SWAPCHAIN_USAGE_SAMPLED_BIT | XR_SWAPCHAIN_USAGE_COLOR_ATTACHMENT_BIT;
    swapchainInfo.format = *found;
    swapchainInfo.sampleCount = 1;
    swapchainInfo.width = BoardWidth;
    swapchainInfo.height = BoardHeight;
    swapchainInfo.faceCount = 1;
    swapchainInfo.arraySize = 1;
    swapchainInfo.mipCount = 1;
    result = xrCreateSwapchain(resources.session, &swapchainInfo, &resources.swapchain);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateSwapchain", result);

    uint32_t imageCount = 0;
    result = xrEnumerateSwapchainImages(resources.swapchain, 0, &imageCount, nullptr);
    if (XR_FAILED(result))
        return Failure(env, "xrEnumerateSwapchainImages", result);
    std::vector<XrSwapchainImageOpenGLESKHR> images(imageCount);
    for (auto& image : images)
        image.type = XR_TYPE_SWAPCHAIN_IMAGE_OPENGL_ES_KHR;
    result = xrEnumerateSwapchainImages(resources.swapchain, imageCount, &imageCount,
        reinterpret_cast<XrSwapchainImageBaseHeader*>(images.data()));
    if (XR_FAILED(result))
        return Failure(env, "xrEnumerateSwapchainImages(list)", result);

    std::vector<XrSwapchainImageOpenGLESKHR> beamImages;
    if (systemProperties.graphicsProperties.maxLayerCount >= 3) {
        swapchainInfo.width = 8;
        swapchainInfo.height = 8;
        if (XR_SUCCEEDED(xrCreateSwapchain(resources.session, &swapchainInfo, &resources.beamSwapchain))) {
            uint32_t beamImageCount = 0;
            if (XR_SUCCEEDED(xrEnumerateSwapchainImages(resources.beamSwapchain, 0, &beamImageCount, nullptr)) &&
                beamImageCount > 0) {
                beamImages.resize(beamImageCount);
                for (auto& image : beamImages)
                    image.type = XR_TYPE_SWAPCHAIN_IMAGE_OPENGL_ES_KHR;
                if (XR_FAILED(xrEnumerateSwapchainImages(resources.beamSwapchain, beamImageCount,
                    &beamImageCount, reinterpret_cast<XrSwapchainImageBaseHeader*>(beamImages.data()))))
                    beamImages.clear();
            }
        }
    }
    if (beamImages.empty())
        __android_log_print(ANDROID_LOG_WARN, LogTag,
            "3D-Strahl nicht verfügbar; XR-Zielpunkt bleibt nutzbar");

    glGenFramebuffers(1, &resources.framebuffer);
    if (resources.framebuffer == 0)
        return Failure(env, "OpenGL-Framebuffer konnte nicht erstellt werden");

    __android_log_print(ANDROID_LOG_INFO, LogTag, "OpenXR-Session und Quad-Swapchain bereit");
    bool running = false;
    bool everReady = false;
    const auto readyDeadline = std::chrono::steady_clock::now() + std::chrono::seconds(30);
    bool boardPlaced = false;
    bool exitRequested = false;
    std::chrono::steady_clock::time_point exitRequestedAt;
    std::chrono::steady_clock::time_point nextZoomAt;
    int lastZoomDirection = 0;
    XrPosef boardPose{};
    boardPose.orientation.w = 1.0f;
    while (true) {
        if (token <= cancelledThrough.load()) {
            if (!running)
                break;

            if (!exitRequested) {
                result = xrRequestExitSession(resources.session);
                if (XR_FAILED(result))
                    return Failure(env, "xrRequestExitSession", result);
                exitRequested = true;
                exitRequestedAt = std::chrono::steady_clock::now();
            } else if (std::chrono::steady_clock::now() - exitRequestedAt > std::chrono::seconds(5)) {
                __android_log_print(ANDROID_LOG_WARN, LogTag,
                    "OpenXR-Session meldet nach Stop-Anforderung kein STOPPING; beende sie direkt");
                break;
            }
        }

        XrEventDataBuffer event{XR_TYPE_EVENT_DATA_BUFFER};
        while ((result = xrPollEvent(resources.instance, &event)) == XR_SUCCESS) {
            if (event.type == XR_TYPE_EVENT_DATA_SESSION_STATE_CHANGED) {
                const auto& changed = *reinterpret_cast<const XrEventDataSessionStateChanged*>(&event);
                if (changed.session == resources.session) {
                    __android_log_print(ANDROID_LOG_INFO, LogTag, "OpenXR-Sessionzustand: %d",
                        static_cast<int>(changed.state));
                    if (changed.state == XR_SESSION_STATE_READY && !running) {
                        XrSessionBeginInfo beginInfo{XR_TYPE_SESSION_BEGIN_INFO};
                        beginInfo.primaryViewConfigurationType = XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO;
                        result = xrBeginSession(resources.session, &beginInfo);
                        if (XR_FAILED(result))
                            return Failure(env, "xrBeginSession", result);
                        running = true;
                        everReady = true;
                        __android_log_print(ANDROID_LOG_INFO, LogTag, "OpenXR-Session läuft");
                    } else if (changed.state == XR_SESSION_STATE_STOPPING && running) {
                        pointer.Update(-1, -1, false, false, false, false);
                        result = xrEndSession(resources.session);
                        if (XR_FAILED(result))
                            return Failure(env, "xrEndSession", result);
                        running = false;
                    } else if (changed.state == XR_SESSION_STATE_EXITING ||
                               changed.state == XR_SESSION_STATE_LOSS_PENDING) {
                        return env->NewStringUTF("OpenXR-Session beendet");
                    }
                }
            }
            else if (event.type == XR_TYPE_EVENT_DATA_REFERENCE_SPACE_CHANGE_PENDING) {
                // Langer Druck auf die Meta-Taste (Recenter): Fläche neu vor den Blick stellen.
                boardPlaced = false;
                __android_log_print(ANDROID_LOG_INFO, LogTag, "Recenter: Quad wird neu platziert");
            }
            event = {XR_TYPE_EVENT_DATA_BUFFER};
        }
        if (result != XR_EVENT_UNAVAILABLE)
            return Failure(env, "xrPollEvent", result);

        if (!running) {
            if (!everReady && std::chrono::steady_clock::now() >= readyDeadline)
                return Failure(env, "OpenXR-Session erhielt innerhalb von 30 s kein READY");
            std::this_thread::sleep_for(std::chrono::milliseconds(16));
            continue;
        }

        XrFrameWaitInfo waitInfo{XR_TYPE_FRAME_WAIT_INFO};
        XrFrameState frameState{XR_TYPE_FRAME_STATE};
        result = xrWaitFrame(resources.session, &waitInfo, &frameState);
        if (XR_FAILED(result))
            return Failure(env, "xrWaitFrame", result);
        XrFrameBeginInfo frameBeginInfo{XR_TYPE_FRAME_BEGIN_INFO};
        result = xrBeginFrame(resources.session, &frameBeginInfo);
        if (XR_FAILED(result))
            return Failure(env, "xrBeginFrame", result);

        if (replaceBoard.exchange(false))
            boardPlaced = false;

        const float boardWidthNow = boardWidthMeters.load();
        const float boardHeightNow = boardWidthNow * OpenRaXr::BoardHeightMeters / OpenRaXr::BoardWidthMeters;
        if (!boardPlaced) {
            XrSpaceLocation viewLocation{XR_TYPE_SPACE_LOCATION};
            const XrResult locateResult = xrLocateSpace(resources.viewSpace, resources.space,
                frameState.predictedDisplayTime, &viewLocation);
            constexpr XrSpaceLocationFlags validPose = XR_SPACE_LOCATION_POSITION_VALID_BIT |
                XR_SPACE_LOCATION_ORIENTATION_VALID_BIT;
            if (XR_SUCCEEDED(locateResult) && (viewLocation.locationFlags & validPose) == validPose) {
                boardPose = OpenRaXr::UprightBoardPose(viewLocation.pose, boardDistance.load());
                boardPose.position.y += boardHeightOffset.load();
                boardPlaced = true;
                __android_log_print(ANDROID_LOG_INFO, LogTag,
                    "Quad aufrecht vor der Blickrichtung platziert");
            }
        }

        int cursorX = -1;
        int cursorY = -1;
        XrVector3f aimOrigin{};
        bool aimValid = false;
        bool triggerPressed = false;
        bool contextPressed = false;
        bool menuSelectPressed = false;
        bool menuBackPressed = false;
        bool panPressed = false;
        bool additive = false;
        float zoomAxis = 0.0f;
        XrActiveActionSet activeActionSet{resources.actionSet, XR_NULL_PATH};
        XrActionsSyncInfo syncInfo{XR_TYPE_ACTIONS_SYNC_INFO};
        syncInfo.countActiveActionSets = 1;
        syncInfo.activeActionSets = &activeActionSet;
        result = xrSyncActions(resources.session, &syncInfo);
        if (XR_SUCCEEDED(result)) {
            XrActionStateGetInfo stateInfo{XR_TYPE_ACTION_STATE_GET_INFO};
            stateInfo.subactionPath = rightHandPath;
            stateInfo.action = aimAction;
            XrActionStatePose aimState{XR_TYPE_ACTION_STATE_POSE};
            if (XR_SUCCEEDED(xrGetActionStatePose(resources.session, &stateInfo, &aimState)) &&
                aimState.isActive) {
                XrSpaceLocation location{XR_TYPE_SPACE_LOCATION};
                const XrResult locateResult = xrLocateSpace(resources.aimSpace, resources.space,
                    frameState.predictedDisplayTime, &location);
                constexpr XrSpaceLocationFlags validPose = XR_SPACE_LOCATION_POSITION_VALID_BIT |
                    XR_SPACE_LOCATION_ORIENTATION_VALID_BIT;
                if (boardPlaced && XR_SUCCEEDED(locateResult) &&
                    (location.locationFlags & validPose) == validPose) {
                    aimOrigin = location.pose.position;
                    aimValid = true;
                    MapAimToBoard(location.pose, boardPose, cursorX, cursorY, boardWidthNow, boardHeightNow);
                }
            }

            stateInfo.action = triggerAction;
            XrActionStateFloat triggerState{XR_TYPE_ACTION_STATE_FLOAT};
            if (XR_SUCCEEDED(xrGetActionStateFloat(resources.session, &stateInfo, &triggerState)) &&
                triggerState.isActive)
                triggerPressed = triggerState.currentState > 0.7f;

            stateInfo.action = contextAction;
            XrActionStateBoolean contextState{XR_TYPE_ACTION_STATE_BOOLEAN};
            if (XR_SUCCEEDED(xrGetActionStateBoolean(resources.session, &stateInfo, &contextState)) &&
                contextState.isActive)
            {
                contextPressed = contextState.currentState == XR_TRUE;
                menuSelectPressed = contextPressed && contextState.changedSinceLastSync;
            }

            stateInfo.action = panAction;
            XrActionStateFloat panState{XR_TYPE_ACTION_STATE_FLOAT};
            if (XR_SUCCEEDED(xrGetActionStateFloat(resources.session, &stateInfo, &panState)) &&
                panState.isActive)
                panPressed = panState.currentState > 0.7f;

            stateInfo.action = additiveAction;
            XrActionStateBoolean additiveState{XR_TYPE_ACTION_STATE_BOOLEAN};
            if (XR_SUCCEEDED(xrGetActionStateBoolean(resources.session, &stateInfo, &additiveState)) &&
                additiveState.isActive)
            {
                additive = additiveState.currentState == XR_TRUE;
                menuBackPressed = additive && additiveState.changedSinceLastSync;
            }

            stateInfo.action = zoomAction;
            XrActionStateFloat zoomState{XR_TYPE_ACTION_STATE_FLOAT};
            if (XR_SUCCEEDED(xrGetActionStateFloat(resources.session, &stateInfo, &zoomState)) &&
                zoomState.isActive)
                zoomAxis = zoomState.currentState;

            stateInfo.subactionPath = leftHandPath;
            stateInfo.action = menuAction;
            XrActionStateBoolean menuState{XR_TYPE_ACTION_STATE_BOOLEAN};
            if (XR_SUCCEEDED(xrGetActionStateBoolean(resources.session, &stateInfo, &menuState)) &&
                menuState.isActive && menuState.changedSinceLastSync && menuState.currentState == XR_TRUE)
                pointer.Emit({OpenRaXr::PointerEventType::MenuToggle, 0, 0});

            stateInfo.action = deployAction;
            XrActionStateBoolean deployState{XR_TYPE_ACTION_STATE_BOOLEAN};
            if (XR_SUCCEEDED(xrGetActionStateBoolean(resources.session, &stateInfo, &deployState)) &&
                deployState.isActive && deployState.changedSinceLastSync && deployState.currentState == XR_TRUE)
                pointer.Emit({OpenRaXr::PointerEventType::Deploy, 0, 0});
        }

        const int pointerY = cursorY < 0 ? -1 : BoardHeight - 1 - cursorY;
        pointer.Update(cursorX, pointerY, triggerPressed, contextPressed, panPressed, additive);

        // A held stick repeats at a deliberate rate instead of sending one scroll per XR frame.
        const int zoomDirection = zoomAxis > 0.65f ? 1 : zoomAxis < -0.65f ? -1 : 0;
        if (zoomDirection == 0)
            lastZoomDirection = 0;
        else if (boardPlaced) {
            const auto now = std::chrono::steady_clock::now();
            if (zoomDirection != lastZoomDirection)
                nextZoomAt = now;
            if (now >= nextZoomAt) {
                pointer.Emit({zoomDirection > 0 ? OpenRaXr::PointerEventType::ScrollUp :
                    OpenRaXr::PointerEventType::ScrollDown,
                    cursorX >= 0 ? cursorX : BoardWidth / 2,
                    pointerY >= 0 ? pointerY : BoardHeight / 2});
                nextZoomAt = now + std::chrono::milliseconds(150);
            }
            lastZoomDirection = zoomDirection;
        }

        // Emit these after board pointer transitions: if A closes the menu,
        // its contextual Down must already have been consumed by the menu.
        // Button actions work even when the controller ray misses the board.
        if (menuSelectPressed)
            pointer.Emit({OpenRaXr::PointerEventType::MenuSelect, cursorX, pointerY});
        if (menuBackPressed)
            pointer.Emit({OpenRaXr::PointerEventType::MenuBack, cursorX, pointerY});

        XrCompositionLayerQuad quad{XR_TYPE_COMPOSITION_LAYER_QUAD};
        XrCompositionLayerQuad beamQuads[2]{{XR_TYPE_COMPOSITION_LAYER_QUAD},
            {XR_TYPE_COMPOSITION_LAYER_QUAD}};
        const XrCompositionLayerBaseHeader* layers[4]{};
        uint32_t layerCount = 0;

        // Passthrough is the bottom layer; the board lets it through wherever its alpha is 0.
        UpdatePassthrough(resources);
        XrCompositionLayerPassthroughFB passthroughLayer{XR_TYPE_COMPOSITION_LAYER_PASSTHROUGH_FB};
        if (resources.passthroughRunning && frameState.shouldRender) {
            passthroughLayer.layerHandle = resources.passthroughLayer;
            passthroughLayer.flags = XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT;
            passthroughLayer.space = XR_NULL_HANDLE;
            layers[layerCount++] = reinterpret_cast<const XrCompositionLayerBaseHeader*>(&passthroughLayer);
        }

        if (frameState.shouldRender && boardPlaced) {
            XrSwapchainImageAcquireInfo acquireInfo{XR_TYPE_SWAPCHAIN_IMAGE_ACQUIRE_INFO};
            uint32_t imageIndex = 0;
            result = xrAcquireSwapchainImage(resources.swapchain, &acquireInfo, &imageIndex);
            if (XR_FAILED(result))
                return Failure(env, "xrAcquireSwapchainImage", result);
            XrSwapchainImageWaitInfo imageWaitInfo{XR_TYPE_SWAPCHAIN_IMAGE_WAIT_INFO};
            imageWaitInfo.timeout = XR_INFINITE_DURATION;
            result = xrWaitSwapchainImage(resources.swapchain, &imageWaitInfo);
            if (XR_FAILED(result))
                return Failure(env, "xrWaitSwapchainImage", result);
            const bool drawn = imageIndex < images.size() &&
                DrawBoard(resources.framebuffer, images[imageIndex].image,
                    cursorX, cursorY, triggerPressed, contextPressed);
            XrSwapchainImageReleaseInfo releaseInfo{XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO};
            result = xrReleaseSwapchainImage(resources.swapchain, &releaseInfo);
            if (XR_FAILED(result))
                return Failure(env, "xrReleaseSwapchainImage", result);
            if (!drawn)
                return Failure(env, "Quad-Testbild konnte nicht gezeichnet werden");

            // Only blend with passthrough underneath; otherwise the board stays fully opaque.
            if (resources.passthroughRunning)
                quad.layerFlags = XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT;
            quad.space = resources.space;
            quad.eyeVisibility = XR_EYE_VISIBILITY_BOTH;
            quad.subImage.swapchain = resources.swapchain;
            quad.subImage.imageRect.extent = {BoardWidth, BoardHeight};
            quad.pose = boardPose;
            quad.size = {boardWidthNow, boardHeightNow};
            layers[layerCount++] = reinterpret_cast<const XrCompositionLayerBaseHeader*>(&quad);

            if (rayVisible.load() && aimValid && cursorX >= 0 && cursorY >= 0 &&
                !beamImages.empty()) {
                XrSwapchainImageAcquireInfo beamAcquire{XR_TYPE_SWAPCHAIN_IMAGE_ACQUIRE_INFO};
                uint32_t beamIndex = 0;
                if (XR_SUCCEEDED(xrAcquireSwapchainImage(resources.beamSwapchain, &beamAcquire, &beamIndex))) {
                    XrSwapchainImageWaitInfo beamWait{XR_TYPE_SWAPCHAIN_IMAGE_WAIT_INFO};
                    beamWait.timeout = XR_INFINITE_DURATION;
                    const bool beamReady = XR_SUCCEEDED(xrWaitSwapchainImage(resources.beamSwapchain, &beamWait)) &&
                        beamIndex < beamImages.size() &&
                        DrawRayTexture(resources.framebuffer, beamImages[beamIndex].image);
                    XrSwapchainImageReleaseInfo beamRelease{XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO};
                    const bool beamReleased = XR_SUCCEEDED(xrReleaseSwapchainImage(resources.beamSwapchain, &beamRelease));
                    if (beamReady && beamReleased) {
                        const auto hit = OpenRaXr::BoardPointFromPixel(boardPose, cursorX, cursorY,
                            boardWidthNow, boardHeightNow);
                        const float beamWidth = 0.005f * (1 + std::clamp(rayThickness.load(), 0, 2));
                        for (int ribbon = 0; ribbon < 2; ++ribbon) {
                            float beamLength = 0;
                            XrPosef beamPose{};
                            if (!OpenRaXr::BeamPose(aimOrigin, hit, boardPose,
                                ribbon != 0, beamPose, beamLength))
                                continue;
                            auto& beam = beamQuads[ribbon];
                            beam.layerFlags = XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT |
                                XR_COMPOSITION_LAYER_UNPREMULTIPLIED_ALPHA_BIT;
                            beam.space = resources.space;
                            beam.eyeVisibility = XR_EYE_VISIBILITY_BOTH;
                            beam.subImage.swapchain = resources.beamSwapchain;
                            beam.subImage.imageRect.extent = {8, 8};
                            beam.pose = beamPose;
                            beam.size = {beamWidth, beamLength};
                            layers[layerCount++] = reinterpret_cast<const XrCompositionLayerBaseHeader*>(&beam);
                        }
                    }
                }
            }
        }

        XrFrameEndInfo endInfo{XR_TYPE_FRAME_END_INFO};
        endInfo.displayTime = frameState.predictedDisplayTime;
        endInfo.environmentBlendMode = XR_ENVIRONMENT_BLEND_MODE_OPAQUE;
        endInfo.layerCount = layerCount;
        endInfo.layers = layerCount != 0 ? layers : nullptr;
        result = xrEndFrame(resources.session, &endInfo);
        if (XR_FAILED(result))
            return Failure(env, "xrEndFrame", result);
    }

    return env->NewStringUTF("OpenXR-Quad-Test beendet");
}
