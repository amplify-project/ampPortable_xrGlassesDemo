# Abstract Mapping Guide

This document explains what the abstract visualisation is showing.

## Overall Reading

The mesh is a dynamic surface driven by audience physiology and behavioural data:

- **Slow global shape** shows tonic electrodermal activity.
- **Particle colour and directional trails** show temperature rate of change.
- **Local sparks and short local deformations** show skin conductance responses.
- **Global particle size, unison pulse, and the perimeter halo** show heart rate.
- **Spatial dispersion, mesh coherence, and lattice solidity** show engagement.

Each stream owns a separate visual channel so one stream does not suppress another. The physiological inputs are signed standardized signals. Values near `0` represent baseline. Positive and negative values represent position relative to that baseline, not necessarily the frame-to-frame direction of travel.

## Behavioural Signal

### Engagement

Engagement controls how solid or dispersed the mesh is.

- **Low engagement**: particles disperse and drift while the mesh loses coherence.
- **High engagement**: particles become spatially coherent and the lattice becomes more solid.

Engagement does not reduce the main particles' size or minimum visibility. This preserves the readability of heart-rate cues at low engagement.

This represents how strongly the audience is collectively held by the performance.

## Physiological Standard Deviations

### Tonic Electrodermal Activity Standard Deviation

This controls the slow, overall pressure of the mesh.

- **Higher positive values** compress the mesh inward and lift its centre upward.
- **Lower negative values** expand the mesh outward and let it sink or relax downward.

This represents slow bodily tension or release.

### Temperature Rate-of-Change Standard Deviation

This controls particle hue and directional trails or travelling colour waves.

- **Positive movement** appears as warm red/orange wave activity moving from left to right.
- **Negative movement** appears as cool blue wave activity moving from right to left.
- Strong changes can trigger short bursts of travelling colour waves.

Temperature does not change the slow global mesh form, main-particle size, or main-particle opacity.

This represents thermal change becoming a visible wave or wash through the audience body.

### Skin Conductance Response Frequency Standard Deviation

This controls sudden events: rings, sparks, and local shocks in the mesh.

- **Higher magnitude** creates more frequent and stronger events.
- **Positive events** push upward and outward.
- **Negative events** pull inward or downward.
- Strong events brighten nearby particles and can create white spark activity.

This represents moments of audience reaction, surprise, stress, or heightened attention.

SCR events do not change the global size of the main particles.

### Heart Rate Standard Deviation

This controls the global size and synchronized pulse of every main particle, plus a two-ring halo around the visualisation.

- **Positive values** enlarge every particle, quicken the pulse, and place the active halo outside the neutral reference ring.
- **Negative values** reduce every particle, slow the pulse, and place the active halo inside the neutral reference ring.
- **Positive pulses** expand outward; **negative pulses** contract inward.
- The active halo never crosses the neutral reference ring, so its sign remains unambiguous during a pulse.
- Every particle and the active halo share one pulse clock and remain in unison.
- If heart-rate data becomes stale, the particles return to neutral and the active halo fades away while the reference remains.


This represents the visible tempo of audience physiology.


