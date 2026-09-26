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
    XrActionSet actionSet = XR_NULL_HANDLE;
    EGLDisplay display = EGL_NO_DISPLAY;
    EGLSurface surface = EGL_NO_SURFACE;
    EGLContext context = EGL_NO_CONTEXT;
    GLuint framebuffer = 0;

    ~Resources()
    {
        if (framebuffer != 0)
            glDeleteFramebuffers(1, &framebuffer);
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
    if (frame) {
        glBindTexture(GL_TEXTURE_2D, texture);
        glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
        glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, BoardWidth, BoardHeight,
            GL_RGBA, GL_UNSIGNED_BYTE, frame->data());
        glBindTexture(GL_TEXTURE_2D, 0);
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
        glScissor(std::max(0, cursorX - 12), std::max(0, cursorY - 12), 24, 24);
        if (pressed)
            glClearColor(0.95f, 0.22f, 0.15f, 1.0f);
        else if (contextPressed)
            glClearColor(0.35f, 0.65f, 1.0f, 1.0f);
        else
            glClearColor(0.95f, 0.90f, 0.35f, 1.0f);
        glClear(GL_COLOR_BUFFER_BIT);
    }
    glDisable(GL_SCISSOR_TEST);
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

    void Update(int x, int y, bool pressed, bool contextPressed)
    {
        pointer.Update(x >= 0 && y >= 0, x, y, pressed, contextPressed,
            [this](OpenRaXr::PointerEvent event) { Emit(event); });
    }

    ~PointerDispatcher()
    {
        pointer.Release([this](OpenRaXr::PointerEvent event) { Emit(event); });
    }
};

} // namespace

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

    constexpr const char* enabledExtensions[] = {
        XR_KHR_ANDROID_CREATE_INSTANCE_EXTENSION_NAME,
        XR_KHR_OPENGL_ES_ENABLE_EXTENSION_NAME,
    };
    for (const char* extension : enabledExtensions)
        if (!Supports(extensions, extension))
            return Failure(env, extension);

    Resources resources;
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
    instanceInfo.enabledExtensionCount = static_cast<uint32_t>(std::size(enabledExtensions));
    instanceInfo.enabledExtensionNames = enabledExtensions;
    result = xrCreateInstance(&instanceInfo, &resources.instance);
    if (XR_FAILED(result))
        return Failure(env, "xrCreateInstance", result);

    XrSystemGetInfo systemInfo{XR_TYPE_SYSTEM_GET_INFO};
    systemInfo.formFactor = XR_FORM_FACTOR_HEAD_MOUNTED_DISPLAY;
    XrSystemId systemId = XR_NULL_SYSTEM_ID;
    result = xrGetSystem(resources.instance, &systemInfo, &systemId);
    if (XR_FAILED(result))
        return Failure(env, "xrGetSystem", result);

    XrPath rightHandPath = XR_NULL_PATH;
    XrPath touchProfile = XR_NULL_PATH;
    XrPath aimBinding = XR_NULL_PATH;
    XrPath triggerBinding = XR_NULL_PATH;
    XrPath contextBinding = XR_NULL_PATH;
    result = xrStringToPath(resources.instance, "/user/hand/right", &rightHandPath);
    if (XR_FAILED(result))
        return Failure(env, "xrStringToPath(right hand)", result);
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

    const XrActionSuggestedBinding bindings[] = {
        {aimAction, aimBinding},
        {triggerAction, triggerBinding},
        {contextAction, contextBinding},
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

    glGenFramebuffers(1, &resources.framebuffer);
    if (resources.framebuffer == 0)
        return Failure(env, "OpenGL-Framebuffer konnte nicht erstellt werden");

    __android_log_print(ANDROID_LOG_INFO, LogTag, "OpenXR-Session und Quad-Swapchain bereit");
    bool running = false;
    bool boardPlaced = false;
    bool exitRequested = false;
    std::chrono::steady_clock::time_point exitRequestedAt;
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
                    if (changed.state == XR_SESSION_STATE_READY && !running) {
                        XrSessionBeginInfo beginInfo{XR_TYPE_SESSION_BEGIN_INFO};
                        beginInfo.primaryViewConfigurationType = XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO;
                        result = xrBeginSession(resources.session, &beginInfo);
                        if (XR_FAILED(result))
                            return Failure(env, "xrBeginSession", result);
                        running = true;
                        __android_log_print(ANDROID_LOG_INFO, LogTag, "OpenXR-Session läuft");
                    } else if (changed.state == XR_SESSION_STATE_STOPPING && running) {
                        pointer.Update(-1, -1, false, false);
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
            event = {XR_TYPE_EVENT_DATA_BUFFER};
        }
        if (result != XR_EVENT_UNAVAILABLE)
            return Failure(env, "xrPollEvent", result);

        if (!running) {
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

        if (!boardPlaced) {
            XrSpaceLocation viewLocation{XR_TYPE_SPACE_LOCATION};
            const XrResult locateResult = xrLocateSpace(resources.viewSpace, resources.space,
                frameState.predictedDisplayTime, &viewLocation);
            constexpr XrSpaceLocationFlags validPose = XR_SPACE_LOCATION_POSITION_VALID_BIT |
                XR_SPACE_LOCATION_ORIENTATION_VALID_BIT;
            if (XR_SUCCEEDED(locateResult) && (viewLocation.locationFlags & validPose) == validPose) {
                boardPose = viewLocation.pose;
                const auto forward = Rotate(boardPose.orientation, {0.0f, 0.0f, -1.4f});
                boardPose.position.x += forward.x;
                boardPose.position.y += forward.y;
                boardPose.position.z += forward.z;
                boardPlaced = true;
                __android_log_print(ANDROID_LOG_INFO, LogTag,
                    "Quad relativ zur ersten gültigen Blickpose platziert");
            }
        }

        int cursorX = -1;
        int cursorY = -1;
        bool triggerPressed = false;
        bool contextPressed = false;
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
                    (location.locationFlags & validPose) == validPose)
                    MapAimToBoard(location.pose, boardPose, cursorX, cursorY);
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
                contextPressed = contextState.currentState == XR_TRUE;
        }

        pointer.Update(cursorX, cursorY < 0 ? -1 : BoardHeight - 1 - cursorY,
            triggerPressed, contextPressed);

        XrCompositionLayerQuad quad{XR_TYPE_COMPOSITION_LAYER_QUAD};
        uint32_t layerCount = 0;
        const XrCompositionLayerBaseHeader* layer = reinterpret_cast<const XrCompositionLayerBaseHeader*>(&quad);
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

            quad.space = resources.space;
            quad.eyeVisibility = XR_EYE_VISIBILITY_BOTH;
            quad.subImage.swapchain = resources.swapchain;
            quad.subImage.imageRect.extent = {BoardWidth, BoardHeight};
            quad.pose = boardPose;
            quad.size = {1.2f, 0.6f};
            layerCount = 1;
        }

        XrFrameEndInfo endInfo{XR_TYPE_FRAME_END_INFO};
        endInfo.displayTime = frameState.predictedDisplayTime;
        endInfo.environmentBlendMode = XR_ENVIRONMENT_BLEND_MODE_OPAQUE;
        endInfo.layerCount = layerCount;
        endInfo.layers = layerCount != 0 ? &layer : nullptr;
        result = xrEndFrame(resources.session, &endInfo);
        if (XR_FAILED(result))
            return Failure(env, "xrEndFrame", result);
    }

    return env->NewStringUTF("OpenXR-Quad-Test beendet");
}
