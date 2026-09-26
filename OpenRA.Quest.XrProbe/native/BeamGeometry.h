/*
 * Copyright (c) The OpenRA Developers and Contributors.
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

#pragma once

#include "BoardGeometry.h"

#include <cmath>

namespace OpenRaXr {

inline XrVector3f Subtract(XrVector3f a, XrVector3f b)
{
    return {a.x - b.x, a.y - b.y, a.z - b.z};
}

inline XrVector3f Cross(XrVector3f a, XrVector3f b)
{
    return {a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x};
}

inline float Length(XrVector3f v)
{
    return std::sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
}

inline XrVector3f Scale(XrVector3f v, float scale)
{
    return {v.x * scale, v.y * scale, v.z * scale};
}

inline XrQuaternionf QuaternionFromAxes(XrVector3f x, XrVector3f y, XrVector3f z)
{
    const float trace = x.x + y.y + z.z;
    XrQuaternionf q{};
    if (trace > 0.0f) {
        const float s = 2.0f * std::sqrt(trace + 1.0f);
        q.w = 0.25f * s;
        q.x = (y.z - z.y) / s;
        q.y = (z.x - x.z) / s;
        q.z = (x.y - y.x) / s;
    } else if (x.x > y.y && x.x > z.z) {
        const float s = 2.0f * std::sqrt(1.0f + x.x - y.y - z.z);
        q.w = (y.z - z.y) / s;
        q.x = 0.25f * s;
        q.y = (y.x + x.y) / s;
        q.z = (z.x + x.z) / s;
    } else if (y.y > z.z) {
        const float s = 2.0f * std::sqrt(1.0f + y.y - x.x - z.z);
        q.w = (z.x - x.z) / s;
        q.x = (y.x + x.y) / s;
        q.y = 0.25f * s;
        q.z = (z.y + y.z) / s;
    } else {
        const float s = 2.0f * std::sqrt(1.0f + z.z - x.x - y.y);
        q.w = (x.y - y.x) / s;
        q.x = (z.x + x.z) / s;
        q.y = (z.y + y.z) / s;
        q.z = 0.25f * s;
    }
    return q;
}

inline XrVector3f BoardPointFromPixel(const XrPosef& board, int pixelX, int pixelY)
{
    const XrVector3f local{
        (static_cast<float>(pixelX) / BoardWidth - 0.5f) * BoardWidthMeters,
        (static_cast<float>(pixelY) / BoardHeight - 0.5f) * BoardHeightMeters,
        0.015f,
    };
    const auto offset = Rotate(board.orientation, local);
    return {board.position.x + offset.x, board.position.y + offset.y, board.position.z + offset.z};
}

// Two perpendicular ribbons make the depth-aligned controller ray visible from
// more viewing angles without changing the board's input geometry.
inline bool BeamPose(XrVector3f origin, XrVector3f target, const XrPosef& board,
    bool secondRibbon, XrPosef& pose, float& length)
{
    const auto delta = Subtract(target, origin);
    length = Length(delta);
    if (length < 0.05f)
        return false;

    const auto axisY = Scale(delta, 1.0f / length);
    auto reference = Rotate(board.orientation, secondRibbon ? XrVector3f{1, 0, 0} : XrVector3f{0, 1, 0});
    auto axisX = Cross(axisY, reference);
    if (Length(axisX) < 0.001f) {
        reference = Rotate(board.orientation, {0, 0, 1});
        axisX = Cross(axisY, reference);
    }
    if (Length(axisX) < 0.001f)
        return false;

    axisX = Scale(axisX, 1.0f / Length(axisX));
    const auto axisZ = Cross(axisX, axisY);
    pose.orientation = QuaternionFromAxes(axisX, axisY, axisZ);
    pose.position = {(origin.x + target.x) * 0.5f, (origin.y + target.y) * 0.5f,
        (origin.z + target.z) * 0.5f};
    return true;
}

} // namespace OpenRaXr
