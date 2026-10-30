package com.amplify.discovery;

import android.app.Activity;
import android.content.Context;
import android.net.nsd.NsdManager;
import android.net.nsd.NsdServiceInfo;
import android.net.wifi.WifiManager;
import android.os.Handler;
import android.os.Looper;

import com.unity3d.player.UnityPlayer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.nio.charset.StandardCharsets;
import java.util.ArrayDeque;
import java.util.Map;

public final class RedisNsdDiscovery {
    private final Activity activity;
    private final String callbackObjectName;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final ArrayDeque<NsdServiceInfo> pendingServices = new ArrayDeque<>();

    private NsdManager nsdManager;
    private NsdManager.DiscoveryListener discoveryListener;
    private WifiManager.MulticastLock multicastLock;
    private boolean discoveryStarted;
    private boolean resolveInProgress;
    private boolean stopRequested;

    public RedisNsdDiscovery(Activity activity, String callbackObjectName) {
        this.activity = activity;
        this.callbackObjectName = callbackObjectName;
    }

    public void start(final String serviceType) {
        mainHandler.post(() -> startOnMainThread(serviceType));
    }

    public void stop() {
        stopRequested = true;
        mainHandler.post(this::stopOnMainThread);
    }

    private void startOnMainThread(String serviceType) {
        if (activity == null || stopRequested) {
            releaseMulticastLock();
            return;
        }

        nsdManager = (NsdManager) activity.getSystemService(Context.NSD_SERVICE);
        if (nsdManager == null) {
            sendError("Android NSD service is unavailable.");
            return;
        }

        acquireMulticastLock();
        discoveryListener = new NsdManager.DiscoveryListener() {
            @Override
            public void onDiscoveryStarted(String registrationType) {
                discoveryStarted = true;
                if (stopRequested) {
                    stopOnMainThread();
                }
            }

            @Override
            public void onServiceFound(NsdServiceInfo serviceInfo) {
                if (stopRequested || serviceInfo == null) {
                    return;
                }

                pendingServices.offer(serviceInfo);
                resolveNext();
            }

            @Override
            public void onServiceLost(NsdServiceInfo serviceInfo) {
                if (serviceInfo != null) {
                    pendingServices.remove(serviceInfo);
                }
            }

            @Override
            public void onDiscoveryStopped(String serviceType) {
                discoveryStarted = false;
                releaseMulticastLock();
            }

            @Override
            public void onStartDiscoveryFailed(String serviceType, int errorCode) {
                discoveryStarted = false;
                sendError("NSD discovery failed to start (code " + errorCode + ").");
                releaseMulticastLock();
            }

            @Override
            public void onStopDiscoveryFailed(String serviceType, int errorCode) {
                discoveryStarted = false;
                sendError("NSD discovery failed to stop cleanly (code " + errorCode + ").");
                releaseMulticastLock();
            }
        };

        try {
            nsdManager.discoverServices(serviceType, NsdManager.PROTOCOL_DNS_SD, discoveryListener);
        } catch (RuntimeException exception) {
            sendError("Could not start NSD discovery: " + exception.getMessage());
            releaseMulticastLock();
        }
    }

    private void resolveNext() {
        if (stopRequested || resolveInProgress || nsdManager == null) {
            return;
        }

        final NsdServiceInfo serviceInfo = pendingServices.poll();
        if (serviceInfo == null) {
            return;
        }

        resolveInProgress = true;
        try {
            nsdManager.resolveService(serviceInfo, new NsdManager.ResolveListener() {
                @Override
                public void onResolveFailed(NsdServiceInfo failedService, int errorCode) {
                    resolveInProgress = false;
                    sendError("Could not resolve Redis service '" + serviceInfo.getServiceName() + "' (code " + errorCode + ").");
                    resolveNext();
                }

                @Override
                public void onServiceResolved(NsdServiceInfo resolvedService) {
                    resolveInProgress = false;
                    sendResolvedService(resolvedService);
                    resolveNext();
                }
            });
        } catch (RuntimeException exception) {
            resolveInProgress = false;
            sendError("Could not resolve Redis service: " + exception.getMessage());
            resolveNext();
        }
    }

    private void sendResolvedService(NsdServiceInfo serviceInfo) {
        if (stopRequested || serviceInfo == null || serviceInfo.getHost() == null) {
            return;
        }

        try {
            JSONObject payload = new JSONObject();
            payload.put("serviceName", serviceInfo.getServiceName());
            payload.put("host", serviceInfo.getHost().getHostAddress());
            payload.put("port", serviceInfo.getPort());

            JSONArray txt = new JSONArray();
            for (Map.Entry<String, byte[]> pair : serviceInfo.getAttributes().entrySet()) {
                JSONObject record = new JSONObject();
                record.put("key", pair.getKey());
                byte[] value = pair.getValue();
                record.put("value", value == null ? "" : new String(value, StandardCharsets.UTF_8));
                txt.put(record);
            }

            payload.put("txt", txt);
            UnityPlayer.UnitySendMessage(callbackObjectName, "OnRedisServiceResolved", payload.toString());
        } catch (Exception exception) {
            sendError("Could not serialize resolved Redis service: " + exception.getMessage());
        }
    }

    private void acquireMulticastLock() {
        try {
            WifiManager wifiManager = (WifiManager) activity.getApplicationContext().getSystemService(Context.WIFI_SERVICE);
            if (wifiManager == null) {
                return;
            }

            multicastLock = wifiManager.createMulticastLock("amplify-redis-discovery");
            multicastLock.setReferenceCounted(false);
            multicastLock.acquire();
        } catch (RuntimeException exception) {
            sendError("Could not acquire Wi-Fi multicast lock: " + exception.getMessage());
        }
    }

    private void stopOnMainThread() {
        pendingServices.clear();
        if (nsdManager != null && discoveryListener != null && discoveryStarted) {
            try {
                nsdManager.stopServiceDiscovery(discoveryListener);
            } catch (RuntimeException exception) {
                sendError("Could not stop NSD discovery: " + exception.getMessage());
            }
        }

        discoveryStarted = false;
        releaseMulticastLock();
    }

    private void releaseMulticastLock() {
        if (multicastLock == null) {
            return;
        }

        try {
            if (multicastLock.isHeld()) {
                multicastLock.release();
            }
        } catch (RuntimeException ignored) {
            // Best-effort cleanup during app pause/shutdown.
        } finally {
            multicastLock = null;
        }
    }

    private void sendError(String message) {
        if (!stopRequested && callbackObjectName != null) {
            UnityPlayer.UnitySendMessage(callbackObjectName, "OnRedisDiscoveryError", message == null ? "Unknown NSD error." : message);
        }
    }
}
