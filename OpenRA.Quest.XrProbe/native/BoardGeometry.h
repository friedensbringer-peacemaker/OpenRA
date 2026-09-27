/*
 * Copyright (c) The OpenRA Developers and Contributors.
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

#pragma once

#include <openxr/openxr.h>

#include <algorithm>
#include <cmath>

namespace OpenRaXr {

constexpr int BoardWidth = 1280;
constexpr int BoardHeight = 800;
constexpr float BoardWidthMeters = 1.6f;
constexpr float BoardHeightMeters = 1.0f;

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

/// Places the board upright at eye height in front of the head. Only the heading (yaw)
/// is taken from the head pose; pitch and roll at session start would tilt and shift it.
inline XrPosef UprightBoardPose(const XrPosef& headPose, float distanceMeters)
{
    const auto forward = Rotate(headPose.orientation, {0.0f, 0.0f, -1.0f});
    const float yaw = (forward.x * forward.x + forward.z * forward.z) > 1e-6f
        ? std::atan2(-forward.x, -forward.z) : 0.0f;

    XrPosef pose{};
    pose.orientation = {0.0f, std::sin(yaw * 0.5f), 0.0f, std::cos(yaw * 0.5f)};
    pose.position = {
        headPose.position.x - std::sin(yaw) * distanceMeters,
        headPose.position.y,
        headPose.position.z - std::cos(yaw) * distanceMeters,
    };
    return pose;
}

inline bool MapAimToBoard(const XrPosef& aimPose, const XrPosef& boardPose,
    int& pixelX, int& pixelY, float widthMeters = BoardWidthMeters, float heightMeters = BoardHeightMeters)
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
    const float u = x / widthMeters + 0.5f;
    const float v = y / heightMeters + 0.5f;
    if (u < 0.0f || u > 1.0f || v < 0.0f || v > 1.0f)
        return false;

    pixelX = std::min(static_cast<int>(u * BoardWidth), BoardWidth - 1);
    pixelY = std::min(static_cast<int>(v * BoardHeight), BoardHeight - 1);
    return true;
}

} // namespace OpenRaXr
