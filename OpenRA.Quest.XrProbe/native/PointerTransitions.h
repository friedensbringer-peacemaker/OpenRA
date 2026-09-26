/*
 * Copyright (c) The OpenRA Developers and Contributors.
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

#pragma once

namespace OpenRaXr {

enum class PointerEventType { Move = 0, Down = 1, Up = 2 };

struct PointerEvent {
    PointerEventType type;
    int x;
    int y;
};

class PointerTransitions {
    int lastX = -1;
    int lastY = -1;
    bool down = false;

public:
    template <typename Emit>
    void Update(bool valid, int x, int y, bool pressed, Emit emit)
    {
        if (!valid) {
            if (down)
                emit(PointerEvent{PointerEventType::Up, lastX, lastY});
            down = false;
            lastX = -1;
            lastY = -1;
            return;
        }

        if (x != lastX || y != lastY) {
            emit(PointerEvent{PointerEventType::Move, x, y});
            lastX = x;
            lastY = y;
        }

        if (pressed && !down) {
            emit(PointerEvent{PointerEventType::Down, x, y});
            down = true;
        } else if (!pressed && down) {
            emit(PointerEvent{PointerEventType::Up, x, y});
            down = false;
        }
    }

    template <typename Emit>
    void Release(Emit emit) { Update(false, 0, 0, false, emit); }
};

} // namespace OpenRaXr
