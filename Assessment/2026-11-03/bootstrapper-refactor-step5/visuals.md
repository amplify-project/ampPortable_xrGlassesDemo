# Batch 2: Visualization spawning and binding

Extracted automatic/manual instantiation, driver precedence, source/visualizer
binding, sensor selection presentation and instance cleanup into
`Presentation/Bootstrap/VisualizationPresenter`. It owns the runtime view lists,
selected targets and HUD. Bootstrapper retains scene references/configuration,
mode eligibility, dependency construction and the ordered cleanup sequence.

The presenter receives its owner GameObject, Redis registry, transient configuration,
and callbacks to existing positioning/label methods. The callbacks keep layout
unchanged until the third batch extracts it. No helper references Bootstrapper's
type. Unity instance creation stays in Presentation; no scene components are added.

Existing tests keep their cases/assertions and target the extracted instance owner.
Selected graph/artist binding, manual graph operation, repeated configuration,
shared pumps, mode transitions and cleanup remain covered.

Validation: **203/203 full PlayMode tests passed**, Unity exit 0. No tests removed
or new Inspector fields. Existing scene edit is unchanged and excluded from this
commit. Packages, scene/prefab assets, visualizers and input are unchanged.
