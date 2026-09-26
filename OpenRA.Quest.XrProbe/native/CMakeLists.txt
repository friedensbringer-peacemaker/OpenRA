cmake_minimum_required(VERSION 3.22)
project(OpenRaQuestXrProbe LANGUAGES CXX)

if(NOT ANDROID)
  message(FATAL_ERROR "OpenRA Quest XR probe must be built with the Android NDK")
endif()

if(NOT EXISTS "${OPENXR_SDK_ROOT}/include/openxr/openxr.h")
  message(FATAL_ERROR "Set OPENXR_SDK_ROOT to the pinned Khronos OpenXR SDK checkout")
endif()

if(NOT EXISTS "${OPENXR_LOADER_SO}")
  message(FATAL_ERROR "Set OPENXR_LOADER_SO to the arm64 Khronos Android loader")
endif()

add_library(openxr_loader SHARED IMPORTED)
set_target_properties(openxr_loader PROPERTIES IMPORTED_LOCATION "${OPENXR_LOADER_SO}")

add_library(openra_xr_probe SHARED XrProbe.cpp XrQuad.cpp)
target_compile_features(openra_xr_probe PRIVATE cxx_std_17)
target_compile_definitions(openra_xr_probe PRIVATE XR_USE_PLATFORM_ANDROID XR_USE_GRAPHICS_API_OPENGL_ES)
target_include_directories(openra_xr_probe PRIVATE "${OPENXR_SDK_ROOT}/include")
target_link_libraries(openra_xr_probe PRIVATE openxr_loader EGL GLESv3 log)
target_link_options(openra_xr_probe PRIVATE "-Wl,-z,max-page-size=16384")
set_target_properties(openra_xr_probe PROPERTIES CXX_VISIBILITY_PRESET hidden VISIBILITY_INLINES_HIDDEN YES)
