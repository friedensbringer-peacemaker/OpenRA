/*
 * Copyright (c) The OpenRA Developers and Contributors.
 * SPDX-License-Identifier: GPL-3.0-or-later
 *
 * This is an isolated Android/OpenXR loader probe. It does not render or
 * create an XR session; the existing OpenRA Android view remains separate.
 */

#include <jni.h>
#include <EGL/egl.h>

#include <openxr/openxr.h>
#include <openxr/openxr_platform.h>

#include <algorithm>
#include <cstdio>
#include <cstring>
#include <vector>

namespace {

bool HasExtension(const std::vector<XrExtensionProperties>& extensions, const char* name)
{
    return std::any_of(extensions.begin(), extensions.end(), [name](const auto& extension) {
        return std::strcmp(extension.extensionName, name) == 0;
    });
}

jstring Result(JNIEnv* env, const char* stage, XrResult result)
{
    char text[160];
    std::snprintf(text, sizeof(text), "%s: XrResult %d", stage, static_cast<int>(result));
    return env->NewStringUTF(text);
}

} // namespace

extern "C" JNIEXPORT jstring JNICALL
Java_com_friedensbringer_openra_xr_XrProbe_inspect(JNIEnv* env, jclass, jobject activity)
{
    if (activity == nullptr)
        return env->NewStringUTF("Android Activity fehlt");

    JavaVM* vm = nullptr;
    if (env->GetJavaVM(&vm) != JNI_OK || vm == nullptr)
        return env->NewStringUTF("JavaVM nicht verfügbar");

    PFN_xrInitializeLoaderKHR initializeLoader = nullptr;
    XrResult result = xrGetInstanceProcAddr(XR_NULL_HANDLE, "xrInitializeLoaderKHR",
        reinterpret_cast<PFN_xrVoidFunction*>(&initializeLoader));
    if (XR_FAILED(result) || initializeLoader == nullptr)
        return Result(env, "xrGetInstanceProcAddr(xrInitializeLoaderKHR)", result);

    XrLoaderInitInfoAndroidKHR loaderInfo{XR_TYPE_LOADER_INIT_INFO_ANDROID_KHR};
    loaderInfo.applicationVM = vm;
    loaderInfo.applicationContext = activity;
    result = initializeLoader(reinterpret_cast<const XrLoaderInitInfoBaseHeaderKHR*>(&loaderInfo));
    if (XR_FAILED(result))
        return Result(env, "xrInitializeLoaderKHR", result);

    uint32_t extensionCount = 0;
    result = xrEnumerateInstanceExtensionProperties(nullptr, 0, &extensionCount, nullptr);
    if (XR_FAILED(result))
        return Result(env, "xrEnumerateInstanceExtensionProperties(count)", result);

    std::vector<XrExtensionProperties> extensions(extensionCount);
    for (auto& extension : extensions)
        extension.type = XR_TYPE_EXTENSION_PROPERTIES;

    result = xrEnumerateInstanceExtensionProperties(nullptr, extensionCount, &extensionCount, extensions.data());
    if (XR_FAILED(result))
        return Result(env, "xrEnumerateInstanceExtensionProperties(list)", result);

    constexpr const char* requiredExtensions[] = {
        XR_KHR_ANDROID_CREATE_INSTANCE_EXTENSION_NAME,
        XR_KHR_OPENGL_ES_ENABLE_EXTENSION_NAME,
    };
    for (const char* extension : requiredExtensions)
        if (!HasExtension(extensions, extension)) {
            char text[160];
            std::snprintf(text, sizeof(text), "OpenXR-Erweiterung fehlt: %s", extension);
            return env->NewStringUTF(text);
        }

    XrInstanceCreateInfoAndroidKHR androidInfo{XR_TYPE_INSTANCE_CREATE_INFO_ANDROID_KHR};
    androidInfo.applicationVM = vm;
    androidInfo.applicationActivity = activity;

    XrInstanceCreateInfo createInfo{XR_TYPE_INSTANCE_CREATE_INFO};
    createInfo.next = &androidInfo;
    std::snprintf(createInfo.applicationInfo.applicationName,
        sizeof(createInfo.applicationInfo.applicationName), "OpenRA Quest XR Probe");
    createInfo.applicationInfo.applicationVersion = 1;
    std::snprintf(createInfo.applicationInfo.engineName,
        sizeof(createInfo.applicationInfo.engineName), "OpenRA");
    createInfo.applicationInfo.engineVersion = 1;
    createInfo.applicationInfo.apiVersion = XR_MAKE_VERSION(1, 0, 0);
    createInfo.enabledExtensionCount = static_cast<uint32_t>(std::size(requiredExtensions));
    createInfo.enabledExtensionNames = requiredExtensions;

    XrInstance instance = XR_NULL_HANDLE;
    result = xrCreateInstance(&createInfo, &instance);
    if (XR_FAILED(result))
        return Result(env, "xrCreateInstance", result);

    XrSystemGetInfo systemInfo{XR_TYPE_SYSTEM_GET_INFO};
    systemInfo.formFactor = XR_FORM_FACTOR_HEAD_MOUNTED_DISPLAY;
    XrSystemId systemId = XR_NULL_SYSTEM_ID;
    result = xrGetSystem(instance, &systemInfo, &systemId);
    if (XR_FAILED(result)) {
        xrDestroyInstance(instance);
        return Result(env, "xrGetSystem", result);
    }

    XrSystemProperties properties{XR_TYPE_SYSTEM_PROPERTIES};
    result = xrGetSystemProperties(instance, systemId, &properties);
    if (XR_FAILED(result)) {
        xrDestroyInstance(instance);
        return Result(env, "xrGetSystemProperties", result);
    }

    char text[320];
    std::snprintf(text, sizeof(text), "OpenXR bereit: %s, System %llu, %u Erweiterungen",
        properties.systemName, static_cast<unsigned long long>(systemId), extensionCount);
    xrDestroyInstance(instance);
    return env->NewStringUTF(text);
}
