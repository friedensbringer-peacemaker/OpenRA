package com.friedensbringer.openra.xr;

import android.app.Activity;
import android.os.Bundle;
import android.util.Log;
import android.widget.TextView;

/** Standalone Quest launcher used only to validate the Android OpenXR loader. */
public final class XrProbeActivity extends Activity {
    private static final String TAG = "OpenRA.XrProbe";
    private TextView status;
    private boolean started;

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

        new Thread(() -> {
            String result;
            try {
                result = XrProbe.inspect(this);
            } catch (Throwable error) {
                result = "OpenXR-Probe fehlgeschlagen: " + error;
            }

            Log.i(TAG, result);
            String finalResult = result;
            runOnUiThread(() -> status.setText(finalResult));
        }, "openra-xr-probe").start();
    }
}
