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

    pointer.Update(true, 512, 256, false, emit);
    pointer.Update(true, 512, 256, false, emit);
    pointer.Update(true, 512, 256, true, emit);
    pointer.Update(true, 520, 255, true, emit);
    pointer.Update(false, 0, 0, true, emit);
    assert(events.size() == 4);
    assert(events[0].type == PointerEventType::Move && events[0].x == 512);
    assert(events[1].type == PointerEventType::Down && events[1].y == 256);
    assert(events[2].type == PointerEventType::Move && events[2].x == 520);
    assert(events[3].type == PointerEventType::Up && events[3].x == 520);

    // Re-entry while held starts a new selection; release must not leave a held button.
    pointer.Update(true, 600, 300, true, emit);
    pointer.Release(emit);
    assert(events.size() == 7);
    assert(events[4].type == PointerEventType::Move);
    assert(events[5].type == PointerEventType::Down);
    assert(events[6].type == PointerEventType::Up);

    std::puts("Pointer transitions: movement, drag, loss and release passed");
}
