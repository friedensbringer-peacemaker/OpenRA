/*
 * Copyright (c) The OpenRA Developers and Contributors.
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

#pragma once

#include <openxr/openxr.h>

#include <algorithm>

namespace OpenRaXr {

constexpr int BoardWidth = 1024;
constexpr int BoardHeight = 512;
constexpr float BoardWidthMeters = 1.2f;
constexpr float BoardHeightMeters = 0.6f;

inline XrVector3f Rotate(const XrQuaternionf& rotation, XrVector3f vector)
{
    const XrVector3f twiceCross{
        2.0f * (rotation.y * vector.z - rotation.z * vector.y),
        2.0f * (rotation.z * vector.x - rotation.x * vector.z),
        2.0f * (rotation.x * vector.y - rotation.y * vector.x),
    };
    return {
        vector.x + rotation.w * twiceCross.x + rotation.y * twiceCross.z - rotation.z * twiceCross.y,
        vector.y + rotation.w * twiceCross.y + rotation.z * twiceCross.x - rotation.x * twiceCross.z,
        vector.z + rotation.w * twiceCross.z + rotation.x * twiceCross.y - rotation.y * twiceCross.x,
    };
}

inline bool MapAimToBoard(const XrPosef& aimPose, const XrPosef& boardPose,
    int& pixelX, int& pixelY)
{
    const XrQuaternionf inverse{-boardPose.orientation.x, -boardPose.orientation.y,
        -boardPose.orientation.z, boardPose.orientation.w};
    const XrVector3f relative{
        aimPose.position.x - boardPose.position.x,
        aimPose.position.y - boardPose.position.y,
        aimPose.position.z - boardPose.position.z,
    };
    const auto origin = Rotate(inverse, relative);
    const auto direction = Rotate(inverse, Rotate(aimPose.orientation, {0.0f, 0.0f, -1.0f}));
    if (direction.z >= -0.00001f)
        return false;

    const float distance = -origin.z / direction.z;
    if (distance < 0.0f)
        return false;

    const float x = origin.x + distance * direction.x;
    const float y = origin.y + distance * direction.y;
    const float u = x / BoardWidthMeters + 0.5f;
    const float v = y / BoardHeightMeters + 0.5f;
    if (u < 0.0f || u > 1.0f || v < 0.0f || v > 1.0f)
        return false;

    pixelX = std::min(static_cast<int>(u * BoardWidth), BoardWidth - 1);
    pixelY = std::min(static_cast<int>(v * BoardHeight), BoardHeight - 1);
    return true;
}

} // namespace OpenRaXr
