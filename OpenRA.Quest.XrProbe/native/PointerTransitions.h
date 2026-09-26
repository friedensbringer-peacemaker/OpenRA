/*
 * Copyright (c) The OpenRA Developers and Contributors.
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

#pragma once

namespace OpenRaXr {

enum class PointerEventType {
    Move = 0,
    Down = 1,
    Up = 2,
    ContextDown = 3,
    ContextUp = 4,
};

struct PointerEvent {
    PointerEventType type;
    int x;
    int y;
};

class PointerTransitions {
    int lastX = -1;
    int lastY = -1;
    bool down = false;
    bool contextDown = false;

public:
    template <typename Emit>
    void Update(bool valid, int x, int y, bool pressed, bool contextPressed, Emit emit)
    {
        if (!valid) {
            if (down)
                emit(PointerEvent{PointerEventType::Up, lastX, lastY});
            if (contextDown)
                emit(PointerEvent{PointerEventType::ContextUp, lastX, lastY});
            down = false;
            contextDown = false;
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

        if (contextPressed && !contextDown) {
            emit(PointerEvent{PointerEventType::ContextDown, x, y});
            contextDown = true;
        } else if (!contextPressed && contextDown) {
            emit(PointerEvent{PointerEventType::ContextUp, x, y});
            contextDown = false;
        }
    }

    template <typename Emit>
    void Release(Emit emit) { Update(false, 0, 0, false, false, emit); }
};

} // namespace OpenRaXr
