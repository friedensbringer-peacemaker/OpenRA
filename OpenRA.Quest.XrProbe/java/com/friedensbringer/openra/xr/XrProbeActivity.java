package com.friedensbringer.openra.xr;

import android.app.Activity;
import android.os.Bundle;
import android.util.Log;
import android.widget.TextView;

/** Standalone Quest launcher used only to validate the Android OpenXR loader. */
public final class XrProbeActivity extends Activity {
    private static final String TAG = "OpenRA.XrProbe";
    private static final int BOARD_WIDTH = 1024;
    private static final int BOARD_HEIGHT = 512;
    private TextView status;
    private boolean started;
    private long sessionToken;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        status = new TextView(this);
        status.setTextSize(24);
        status.setText("OpenXR-Runtime wird geprüft …");
        setContentView(status);
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (started)
            return;

        started = true;
        sessionToken = XrProbe.beginSession();

        new Thread(() -> {
            String result;
            try {
                if (!XrProbe.submitFrame(createSampleFrame()))
                    result = "RGBA-Testbild wurde abgelehnt";
                else
                    result = XrProbe.showQuad(this, sessionToken);
            } catch (Throwable error) {
                result = "OpenXR-Probe fehlgeschlagen: " + error;
            }

            Log.i(TAG, result);
            String finalResult = result;
            runOnUiThread(() -> status.setText(finalResult));
        }, "openra-xr-probe").start();
    }

    private static byte[] createSampleFrame() {
        byte[] rgba = new byte[BOARD_WIDTH * BOARD_HEIGHT * 4];
        for (int y = 0; y < BOARD_HEIGHT; y++) {
            for (int x = 0; x < BOARD_WIDTH; x++) {
                int offset = (y * BOARD_WIDTH + x) * 4;
                boolean border = x < 12 || x >= BOARD_WIDTH - 12 ||
                    y < 12 || y >= BOARD_HEIGHT - 12;
                boolean middle = Math.abs(x - BOARD_WIDTH / 2) < 5 ||
                    Math.abs(y - BOARD_HEIGHT / 2) < 5;
                rgba[offset] = (byte) (border || middle ? 240 : x < BOARD_WIDTH / 2 ? 40 : 35);
                rgba[offset + 1] = (byte) (border || middle ? 225 : y < BOARD_HEIGHT / 2 ? 85 : 150);
                rgba[offset + 2] = (byte) (border || middle ? 75 : x < BOARD_WIDTH / 2 ? 170 : 75);
                rgba[offset + 3] = (byte) 255;
            }
        }
        return rgba;
    }

    @Override
    protected void onDestroy() {
        try {
            if (sessionToken != 0)
                XrProbe.requestStop(sessionToken);
        } catch (Throwable error) {
            Log.w(TAG, "Native OpenXR-Probe konnte nicht gestoppt werden", error);
        }
        super.onDestroy();
    }
}
