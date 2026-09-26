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
    PanDown = 5,
    PanUp = 6,
    ShiftOn = 7,
    ShiftOff = 8,
    ScrollUp = 9,
    ScrollDown = 10,
};

struct PointerEvent {
    PointerEventType type;
    int x;
    int y;
};

class PointerTransitions {
    enum class Button { None, Select, Context, Pan };
    int lastX = -1;
    int lastY = -1;
    Button button = Button::None;
    bool shift = false;

    static PointerEventType DownEvent(Button button)
    {
        switch (button) {
            case Button::Select: return PointerEventType::Down;
            case Button::Context: return PointerEventType::ContextDown;
            case Button::Pan: return PointerEventType::PanDown;
            case Button::None: break;
        }
        return PointerEventType::Move;
    }

    static PointerEventType UpEvent(Button button)
    {
        switch (button) {
            case Button::Select: return PointerEventType::Up;
            case Button::Context: return PointerEventType::ContextUp;
            case Button::Pan: return PointerEventType::PanUp;
            case Button::None: break;
        }
        return PointerEventType::Move;
    }

public:
    template <typename Emit>
    void Update(bool valid, int x, int y, bool pressed, bool contextPressed,
        bool panPressed, bool additive, Emit emit)
    {
        if (additive != shift) {
            emit(PointerEvent{additive ? PointerEventType::ShiftOn : PointerEventType::ShiftOff, 0, 0});
            shift = additive;
        }

        if (!valid) {
            if (button != Button::None)
                emit(PointerEvent{UpEvent(button), lastX, lastY});
            button = Button::None;
            lastX = -1;
            lastY = -1;
            return;
        }

        if (x != lastX || y != lastY) {
            emit(PointerEvent{PointerEventType::Move, x, y});
            lastX = x;
            lastY = y;
        }

        const Button desired = panPressed ? Button::Pan :
            pressed ? Button::Select : contextPressed ? Button::Context : Button::None;
        if (desired != button) {
            if (button != Button::None)
                emit(PointerEvent{UpEvent(button), x, y});
            if (desired != Button::None)
                emit(PointerEvent{DownEvent(desired), x, y});
            button = desired;
        }
    }

    template <typename Emit>
    void Release(Emit emit) { Update(false, 0, 0, false, false, false, false, emit); }
};

} // namespace OpenRaXr
