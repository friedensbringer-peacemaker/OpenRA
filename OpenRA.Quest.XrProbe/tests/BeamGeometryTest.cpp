/*
 * Copyright (c) The OpenRA Developers and Contributors.
 * SPDX-License-Identifier: GPL-3.0-or-later
 */
#include "../native/BeamGeometry.h"

#include <cassert>
#include <cmath>
#include <cstdio>
#include <initializer_list>

int main()
{
    XrPosef board{};
    board.orientation.w = 1.0f;
    board.position = {0, 0, -1.4f};
    const auto center = OpenRaXr::BoardPointFromPixel(board,
        OpenRaXr::BoardWidth / 2, OpenRaXr::BoardHeight / 2);
    assert(std::fabs(center.x) < 0.002f && std::fabs(center.y) < 0.002f);
    assert(std::fabs(center.z + 1.385f) < 0.002f);

    const XrVector3f origin{0.25f, -0.3f, -0.35f};
    for (bool second : {false, true}) {
        XrPosef beam{};
        float length = 0;
        assert(OpenRaXr::BeamPose(origin, center, board, second, beam, length));
        assert(length > 1.0f && length < 1.2f);
        assert(std::fabs(beam.position.x - 0.125f) < 0.002f);
        const auto direction = OpenRaXr::Rotate(beam.orientation, {0, 1, 0});
        const auto expected = OpenRaXr::Scale(OpenRaXr::Subtract(center, origin), 1.0f / length);
        assert(OpenRaXr::Length(OpenRaXr::Subtract(direction, expected)) < 0.002f);
    }
    std::puts("Beam geometry: board hit, midpoint, length and both ribbons passed");
}
