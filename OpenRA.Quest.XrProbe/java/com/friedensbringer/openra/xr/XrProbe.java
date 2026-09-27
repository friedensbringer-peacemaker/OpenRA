package com.friedensbringer.openra.xr;

import android.app.Activity;
import android.util.Log;

/** Minimal JNI entry point for the native Quest/OpenXR loader probe. */
public final class XrProbe {
    public static final int POINTER_MOVE = 0;
    public static final int POINTER_DOWN = 1;
    public static final int POINTER_UP = 2;
    public static final int POINTER_CONTEXT_DOWN = 3;
    public static final int POINTER_CONTEXT_UP = 4;
    public static final int POINTER_PAN_DOWN = 5;
    public static final int POINTER_PAN_UP = 6;
    public static final int POINTER_SHIFT_ON = 7;
    public static final int POINTER_SHIFT_OFF = 8;
    public static final int POINTER_SCROLL_UP = 9;
    public static final int POINTER_SCROLL_DOWN = 10;
    public static final int POINTER_MENU_TOGGLE = 11;
    public static final int POINTER_DEPLOY = 12;
    public static final int POINTER_MENU_SELECT = 13;
    public static final int POINTER_MENU_BACK = 14;

    /** Runs on the OpenXR thread; consumers must marshal work to their game thread. */
    public interface PointerListener {
        void onPointerEvent(int type, int x, int y);
    }

    private static volatile PointerListener pointerListener;

    static {
        System.loadLibrary("openra_xr_probe");
    }

    private XrProbe() {}

    public static native String inspect(Activity activity);
    public static native long beginSession();
    public static native String showQuad(Activity activity, long sessionToken);
    /** Exactly 1280 x 800 RGBA8 pixels, row zero at the bottom of the quad. */
    public static native boolean submitFrame(byte[] rgba);
    public static native void setPointerStyle(boolean visible, int thickness, int color, int target);
    /** Board distance and width in meters, vertical offset from eye height in meters. */
    public static native void setBoardLayout(float distance, float width, float heightOffset);
    /** Places the board in front of the current gaze on the next frame. */
    public static native void requestRecenter();
    public static native void requestStop(long sessionToken);

    public static void setPointerListener(PointerListener listener) {
        pointerListener = listener;
    }

    /** Called by XrQuad.cpp with top-left 1280 x 800 pixel coordinates. */
    private static void onPointerEvent(int type, int x, int y) {
        if (type != POINTER_MOVE)
            Log.i("OpenRA.XrProbe", "XR pointer " + type + " at (" + x + ", " + y + ")");

        PointerListener listener = pointerListener;
        if (listener != null) {
            try {
                listener.onPointerEvent(type, x, y);
            } catch (RuntimeException error) {
                Log.e("OpenRA.XrProbe", "XR pointer listener failed", error);
            }
        }
    }
}
