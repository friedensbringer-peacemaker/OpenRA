package com.friedensbringer.openra.xr;

import android.app.Activity;

/** Minimal JNI entry point for the native Quest/OpenXR loader probe. */
public final class XrProbe {
    static {
        System.loadLibrary("openra_xr_probe");
    }

    private XrProbe() {}

    public static native String inspect(Activity activity);
    public static native String showQuad(Activity activity);
    public static native void requestStop();
}
