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
    public static native String showQuad(Activity activity);
    /** Exactly 1024 x 512 RGBA8 pixels, row zero at the bottom of the quad. */
    public static native boolean submitFrame(byte[] rgba);
    public static native void requestStop();

    public static void setPointerListener(PointerListener listener) {
        pointerListener = listener;
    }

    /** Called by XrQuad.cpp with top-left 1024 x 512 pixel coordinates. */
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
