/*
 * Copyright (c) The OpenRA Developers and Contributors.
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

#include "BoardGeometry.h"

#include <cassert>
#include <cstdio>

namespace {

XrPosef AimAtLocalPoint(const XrPosef& board, XrVector3f localOrigin)
{
    const auto offset = OpenRaXr::Rotate(board.orientation, localOrigin);
    XrPosef aim{};
    aim.orientation = board.orientation;
    aim.position = {board.position.x + offset.x, board.position.y + offset.y,
        board.position.z + offset.z};
    return aim;
}

void CheckBoard(const XrPosef& board)
{
    int x = -1;
    int y = -1;
    assert(OpenRaXr::MapAimToBoard(AimAtLocalPoint(board, {0.0f, 0.0f, 1.4f}),
        board, x, y));
    assert(x == OpenRaXr::BoardWidth / 2 && y == OpenRaXr::BoardHeight / 2);

    assert(OpenRaXr::MapAimToBoard(AimAtLocalPoint(board, {OpenRaXr::BoardWidthMeters / 2, OpenRaXr::BoardHeightMeters / 2, 1.4f}),
        board, x, y));
    assert(x == OpenRaXr::BoardWidth - 1 && y == OpenRaXr::BoardHeight - 1);

    assert(!OpenRaXr::MapAimToBoard(AimAtLocalPoint(board, {OpenRaXr::BoardWidthMeters / 2 + 0.1f, 0.0f, 1.4f}),
        board, x, y));
    assert(!OpenRaXr::MapAimToBoard(AimAtLocalPoint(board, {0.0f, 0.0f, -1.0f}),
        board, x, y));
}

} // namespace

int main()
{
    XrPosef forward{};
    forward.orientation.w = 1.0f;
    forward.position = {0.0f, 0.0f, -1.4f};
    CheckBoard(forward);

    XrPosef rotated{};
    rotated.orientation = {0.0f, 0.70710678118f, 0.0f, 0.70710678118f};
    rotated.position = {-1.4f, 0.25f, 0.0f};
    CheckBoard(rotated);

    std::puts("Board geometry: identity and rotated poses passed");
}
