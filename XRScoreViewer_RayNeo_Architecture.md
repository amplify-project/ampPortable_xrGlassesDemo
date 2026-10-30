# XR Score Viewer RayNeo Architecture

## Purpose

XR Score Viewer is an augmented-reality data-visualisation system for performers wearing AR glasses (RayNeo X3 Pro & XReal Air 2 Ultra). The system receives live data from six sensors placed around a square performance space and uses that data to drive several visualisation types.

The main design challenge is that the RayNeo glasses may not hold virtual objects steadily in fixed real-world positions during fast head movement. To protect performer comfort, the visualisation should remain stable in the performer's field of view, while the system uses the performer's approximate position only to decide which sensor stream is most relevant.

## Core Idea

The system separates two jobs that are often combined in AR:

- **Visual stability:** where the graphic appears in the glasses.
- **Sensor selection:** which physical sensor is currently driving the graphic.

The graphic can be displayed in a stable 3 DoF or 0 DoF mode, meaning it remains comfortably visible to the performer instead of being locked to a drifting world position. At the same time, the app can still use approximate performer movement through the space to switch between the six sensor streams.

In practical terms:

- The performer sees a stable visual display.
- The app knows the approximate layout of the performance space.
- The app estimates which sensor zone the performer is inside.
- The active data source changes as the performer moves through the space.

## Performance Space Model

The performance space is treated as a square.

At startup, the app is given:

- The size of the square performance space.
- A starting point or origin.
- A forward direction, such as the direction the performer faces at the start.
- The relative positions of the six sensors around the perimeter.

The sensors do not need to be placed at fixed distances from the performer in every venue. Instead, they are positioned relative to the square. For example, they may be arranged around the perimeter as front-left, front-right, right, back-right, back-left, and left.

If the space changes size, the same layout can be scaled to the new square. A small studio and a larger hall can use the same relative layout, with the actual sensor positions recalculated from the dimensions entered at setup.

## Startup Calibration

Space dimensions alone are not enough. The app also needs to align the RayNeo tracking space with the real performance space.

A simple startup calibration could be:

1. The performer stands on a known starting mark.
2. The performer faces a known direction.
3. The app records this as the origin and forward direction.
4. The app calculates the expected positions of the six perimeter sensors.

This gives the app a shared map:

- Where the performer started.
- Which direction is forward.
- Where the sensor zones are.
- How RayNeo movement roughly relates to the performance space.

If this RayNeo position estimate is not reliable enough, the same architecture can later use a UWB tag system or another external positioning method without changing the visual concept.

## Sensor Zones

Each sensor has a zone around it. When the performer enters that zone, the system can make that sensor the active data source.

The zone does not need to be extremely precise. In fact, it should be deliberately generous because RayNeo position tracking may drift or jitter. The goal is not to know the performer's exact location to the centimetre. The goal is to know which sensor region they are probably near.

For six sensors around a square, the system can calculate the performer's distance from each sensor and choose the most appropriate active sensor.

## Stable Switching With Hysteresis

The app should not switch sensors the instant another sensor becomes slightly closer. That would make the display feel nervous and unpredictable.

Instead, the system should use hysteresis. In this context, hysteresis means the current sensor remains active until there is strong evidence that the performer has moved into another sensor's area.

The switching behaviour should include:

- **Enter distance:** how close the performer must be before a sensor can become active.
- **Exit distance:** how far the performer must move away before the current sensor can be released.
- **Dwell time:** how long the performer must remain near a new sensor before the system switches.
- **Cooldown time:** a short delay after switching before another switch is allowed.
- **Minimum improvement:** a new sensor must be clearly closer than the current one, not just slightly closer.

This prevents rapid flickering between nearby sensors. For example, if the performer is between Sensor 2 and Sensor 3, the app should not switch back and forth every time the headset position jitters. It should hold the current sensor until the performer has clearly entered the next zone.

## Visual Display Modes

The visuals can be displayed in either:

- **0 DoF mode:** the graphic is fixed in the performer's field of view.
- **3 DoF mode:** the graphic remains at a stable distance and orientation relative to the performer's view, but can preserve some directional feeling.

Both modes avoid relying on unstable world-locked placement. This should reduce drift, lag, and nausea risk during fast head movement.

The selected sensor changes the content of the visual, not its physical position. The performer may see:

- A main active visualisation.
- A label showing the active sensor.
- Small indicators for the other sensors.
- A subtle transition when the data source changes.

## Visualisation Types

The performer can use the RayNeo touch controls to switch between visualisation types, such as:

- Abstract particle mesh.
- Objective data graphs.
- Standard notation or score material.

This is separate from sensor selection.

The system therefore has two independent controls:

- **Where am I in the space?** This selects the active sensor stream.
- **What type of visual do I want to see?** This selects the presentation style.

Keeping these separate makes the experience clearer and easier to troubleshoot.

## Data Flow

The recommended data flow is:

1. Six sensors send live data streams to the app.
2. The app keeps the latest data from all six streams available.
3. The app estimates the performer's current zone.
4. The sensor router selects the active sensor.
5. The active sensor's data drives the currently selected visualisation.
6. The visualisation is displayed in stable 3 DoF or 0 DoF mode.

The app should avoid disconnecting and reconnecting sensor streams every time the performer changes zones. It is better to keep all streams active and switch which cached stream feeds the visible visualisation. This should make transitions faster and more reliable.

## Suggested System Components

The architecture can be understood as five cooperating parts:

| Component | Role |
| --- | --- |
| Performance Space Setup | Stores the square dimensions, origin, forward direction, and sensor layout. |
| Performer Position Source | Provides an approximate performer position, initially from RayNeo SLAM. |
| Sensor Zone Router | Decides which sensor is active using distance, hysteresis, dwell time, and cooldown. |
| Stable Visual Rig | Keeps the chosen visualisation steady in the performer's view. |
| Visualisation Selector | Lets the performer switch between particle mesh, graphs, and notation. |

If RayNeo SLAM is not accurate enough, the Performer Position Source can later be replaced with UWB or another tracking system. The rest of the architecture can remain largely the same.

## Fallback Positioning Options

The first version can test RayNeo SLAM as the performer position source. If this is too unstable for zone selection, possible fallbacks include:

- UWB tag worn by the performer.
- External tracking system.

The visual display can remain stable in all cases. Only the source of the zone-selection information changes.

## Performer Journey

### 1. Arrival And Setup

The performer arrives in the performance space wearing the RayNeo glasses. Six sensors have been placed around the perimeter of the square space.

The operator or performer enters the size of the performance space into the app. The app uses this size to calculate where each of the six sensor zones should be.

### 2. Calibration

The performer stands on the starting mark and faces the agreed starting direction. The app records this as the reference point for the performance.

From this moment, the app has a rough map of the square space and can estimate where the performer is moving relative to the six sensors.

### 3. Visual Check

The performer sees a stable visual display in the glasses. The display is not fixed to a drifting point in the room. It stays comfortably in view.

The performer or operator confirms:

- The active sensor label is visible.
- The visual is stable during head movement.
- Touch controls can switch between visualisation types.
- Sensor zones are being detected as expected.

### 4. Performance Begins

As the performer moves through the square, the app estimates which sensor zone they are near.

When the performer clearly enters a sensor zone, that sensor becomes the active data source. The visualisation continues to remain stable in the glasses, but the data shaping the visual changes.

For example:

- Near Sensor 1, the particle mesh responds to Sensor 1.
- Near Sensor 2, the visual transitions to Sensor 2.
- In the centre or between sensors, the app can hold the previous sensor until the next zone is clear.

### 5. Switching Visualisation Type

During the performance, the performer can use the RayNeo touch controls to change what kind of visual material is shown.

They might switch from:

- Particle mesh to objective graphs.
- Graphs to notation.
- Notation back to particle mesh.

This does not change which sensor is active. It only changes how the current sensor's data is presented.

### 6. Handling Movement And Uncertainty

If the performer moves quickly or turns their head sharply, the visual remains stable because it is not locked to a fragile world position.

If RayNeo tracking jitters, the sensor router does not immediately switch. Hysteresis, dwell time, and cooldown keep the active sensor stable unless the performer has clearly moved into another zone.

### 7. Optional Recalibration

If the system drifts during a long performance or rehearsal, the performer can return to the starting mark and the operator can recalibrate the origin.

This recentres the app's understanding of the space without changing the sensor data system or visualisation design.

### 8. End Of Performance

At the end of the performance, the app can stop receiving live streams, clear the active visual state, and save any relevant setup profile for future use.

If the same space is used again, the dimensions and sensor layout can be reused. If the performance moves to a new square space, the same relative layout can be scaled to the new dimensions.

## Why This Should Work

This approach should work because it avoids asking the RayNeo glasses to do the thing they are weakest at: holding multiple graphics perfectly still in world space during fast performer movement.

Instead, the glasses are used for stable visual presentation, while approximate position is used only for a lower-risk decision: which sensor stream should currently be foregrounded.

The result preserves the artistic idea of moving through a field of sensor-driven graphics, while reducing visual drift and performer discomfort.

## Key Technical Risks To Test

The first prototype should test:

- Whether RayNeo SLAM is accurate enough for coarse zone selection.
- Whether the visual remains comfortable in 3 DoF or 0 DoF mode during fast head movement.
- Whether hysteresis prevents unwanted sensor switching.
- Whether all six sensor streams can stay connected while the active stream changes.
- Whether the performer can reliably use the RayNeo touch controls during performance.

If those tests pass, the architecture is viable. If RayNeo position tracking fails, the same design can still work with UWB or another external positioning method.
