# Abstract Mapping Guide

This document explains what the abstract visualisation is showing.

## Overall Reading

The mesh is a dynamic surface driven by audience physiology and behavioural data:

- **Shape** shows slow bodily pressure and temperature movement.
- **Pulse** shows heart rhythm and rhythm instability.
- **Sparks and sudden rings** show skin conductance responses.
- **Colour** shows emotional valence.
- **Motion intensity** shows arousal.
- **Solidity and coherence** shows engagement.

The physiological inputs are standard-deviation signals. In practice, they describe how much a metric is varying. Values near `0` are calm or baseline. Larger positive or negative values create stronger visual changes. Positive and negative values are used as directions.

## Affect Signals

### Valence

Valence controls the base colour of the mesh.

- **Low valence**: red/orange tones.
- **Neutral valence**: green/cyan tones.
- **High valence**: yellow/gold tones.

This represents the emotional tone of the audience response.

### Arousal

Arousal controls how active the mesh feels.

- **Low arousal**: slower, smoother, calmer movement.
- **High arousal**: faster motion, more turbulence, sharper changes, stronger trails.

This represents audience activation or intensity.

### Engagement

Engagement controls how solid or dispersed the mesh is.

- **Low engagement**: the mesh becomes looser, dimmer, smaller, and more drifting.
- **High engagement**: the mesh becomes brighter, larger, more coherent, and more solid.

This represents how strongly the audience is collectively held by the performance.

## Physiological Standard Deviations

### Tonic Electrodermal Activity Standard Deviation

This controls the slow, overall pressure of the mesh.

- **Higher positive values** compress the mesh inward and lift its centre upward.
- **Lower negative values** expand the mesh outward and let it sink or relax downward.

This represents slow bodily tension or release.

### Temperature Rate-of-Change Standard Deviation

This controls contour waves across the surface.

- **Larger magnitude** creates taller, more frequent surface contours.
- **Positive movement** appears as warm red/orange wave activity moving from left to right.
- **Negative movement** appears as cool blue wave activity moving from right to left.
- Strong changes can trigger short bursts of travelling colour waves.

This represents thermal change becoming a visible wave or wash through the audience body.

### Skin Conductance Response Frequency Standard Deviation

This controls sudden events: rings, sparks, and local shocks in the mesh.

- **Higher magnitude** creates more frequent and stronger events.
- **Positive events** push upward and outward.
- **Negative events** pull inward or downward.
- Strong events brighten nearby particles and can create white spark activity.

This represents moments of audience reaction, surprise, stress, or heightened attention.

### Heart Rate Standard Deviation

This controls the rhythmic pulse layer.

- **Positive values** quicken the pulse rate.
- **Negative values** slow the pulse rate.
- **Larger magnitude** makes the breathing/pulse layer more visible and energetic.


This represents the visible tempo of audience physiology.

### Inter-Beat Interval Standard Deviation

This controls how regular or broken the pulse rings feel.

- **Positive values** make pulse rings broader and more spacious.
- **Negative values** make pulse rings tighter, more segmented, and more broken.

This represents rhythmic stability versus fragmentation.


