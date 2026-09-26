/*
 * Copyright (c) The OpenRA Developers and Contributors.
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

#include "PointerTransitions.h"

#include <cassert>
#include <cstdio>
#include <vector>

int main()
{
    OpenRaXr::PointerTransitions pointer;
    std::vector<OpenRaXr::PointerEvent> events;
    auto emit = [&](OpenRaXr::PointerEvent event) { events.push_back(event); };
    using OpenRaXr::PointerEventType;

    pointer.Update(true, 512, 256, false, false, false, false, emit);
    pointer.Update(true, 512, 256, false, false, false, false, emit);
    pointer.Update(true, 512, 256, true, false, false, false, emit);
    pointer.Update(true, 520, 255, true, false, false, false, emit);
    pointer.Update(false, 0, 0, true, false, false, false, emit);
    assert(events.size() == 4);
    assert(events[0].type == PointerEventType::Move && events[0].x == 512);
    assert(events[1].type == PointerEventType::Down && events[1].y == 256);
    assert(events[2].type == PointerEventType::Move && events[2].x == 520);
    assert(events[3].type == PointerEventType::Up && events[3].x == 520);

    // Re-entry while held starts a new selection; release must not leave a held button.
    pointer.Update(true, 600, 300, true, false, false, false, emit);
    pointer.Release(emit);
    assert(events.size() == 7);
    assert(events[4].type == PointerEventType::Move);
    assert(events[5].type == PointerEventType::Down);
    assert(events[6].type == PointerEventType::Up);

    // Context clicks use an independent button and release on tracking loss.
    pointer.Update(true, 100, 200, false, true, false, false, emit);
    pointer.Update(true, 110, 200, false, true, false, false, emit);
    pointer.Update(false, 0, 0, false, true, false, false, emit);
    assert(events.size() == 11);
    assert(events[7].type == PointerEventType::Move);
    assert(events[8].type == PointerEventType::ContextDown);
    assert(events[9].type == PointerEventType::Move);
    assert(events[10].type == PointerEventType::ContextUp && events[10].x == 110);

    // B holds Shift while selecting; grip temporarily takes over as middle-drag.
    pointer.Update(true, 300, 250, true, false, false, true, emit);
    pointer.Update(true, 310, 250, true, false, true, true, emit);
    pointer.Update(true, 320, 250, true, false, false, false, emit);
    pointer.Release(emit);
    assert(events.size() == 22);
    assert(events[11].type == PointerEventType::ShiftOn);
    assert(events[12].type == PointerEventType::Move);
    assert(events[13].type == PointerEventType::Down);
    assert(events[14].type == PointerEventType::Move);
    assert(events[15].type == PointerEventType::Up);
    assert(events[16].type == PointerEventType::PanDown);
    assert(events[17].type == PointerEventType::ShiftOff);
    assert(events[18].type == PointerEventType::Move);
    assert(events[19].type == PointerEventType::PanUp);
    assert(events[20].type == PointerEventType::Down);
    assert(events[21].type == PointerEventType::Up);

    std::puts("Pointer transitions: movement, drag, pan, modifier, loss and release passed");
}
