using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Particle field whose particles also act as vertices for a deforming mesh.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(ParticleSystem))]
    [AddComponentMenu("Amp Portable Data Viz/Visualization/Particle Mesh Visualizer")]
    public sealed class ParticleMeshVisualizer : MonoBehaviour, IVisualizer<ParticleMeshSignalSample>
    {
        private const float Tau = Mathf.PI * 2f;

        [Header("Legacy Manual Source (optional)")]
        [SerializeField] private ParticleMeshManualDriver manualDriver;

        [Header("Mesh")]
        [SerializeField, Range(4, 96)] private int gridWidth = 32;
        [SerializeField, Range(4, 96)] private int gridHeight = 32;
        [SerializeField, Range(0.1f, 5f)] private float visualScale = 1.25f;
        [SerializeField, Range(0.01f, 2f)] private float meshCoherence = 0.75f;

        [Header("Motion")]
        [SerializeField, Range(0.05f, 20f)] private float signalSmoothing = 6f;
        [SerializeField, Range(0.01f, 5f)] private float slowFormSmoothing = 0.35f;
        [SerializeField, Range(0.01f, 5f)] private float baseMorphSmoothTime = 0.45f;
        [SerializeField, Range(0f, 3f)] private float turbulenceStrength = 0.35f;
        [SerializeField, Range(0f, 3f)] private float pulseStrength = 0.2f;
        [SerializeField, Range(0f, 5f)] private float internalTimeScale = 1f;

        [Header("SCR Events")]
        [SerializeField, Range(1, 32)] private int maxScrEvents = 12;
        [SerializeField, Range(0.1f, 5f)] private float scrEventLifetime = 1.4f;
        [SerializeField, Range(0.01f, 1f)] private float scrEventRadius = 0.22f;
        [SerializeField, Range(0f, 1f)] private float scrEventThreshold = 0.55f;
        [SerializeField, Range(0f, 10f)] private float scrEventsPerSecondAtMax = 3f;
        [SerializeField, Range(0f, 10f)] private float scrRiseEventGain = 4f;
        [SerializeField, Range(0f, 1f)] private float scrEventDisplacement = 0.16f;

        [Header("SCR Sparks")]
        [SerializeField] private ParticleSystem scrSparkParticleSystem;
        [SerializeField, Range(0, 256)] private int maxScrSparks = 96;
        [SerializeField, Range(0, 16)] private int scrSparksPerEventAtMax = 8;
        [SerializeField, Range(0.05f, 2f)] private float scrSparkLifetime = 0.45f;
        [SerializeField, Range(0.001f, 0.12f)] private float scrSparkSize = 0.025f;
        [SerializeField, Range(0f, 0.5f)] private float scrSparkSpread = 0.08f;
        [SerializeField, Range(0f, 1f)] private float scrSparkLift = 0.28f;
        [SerializeField] private Color scrSparkColor = Color.white;

        [Header("Rhythm Layer")]
        [SerializeField, Range(20f, 180f)] private float rhythmBaseBpm = 72f;
        [SerializeField, Range(0f, 2f)] private float rhythmRateStdDevBoost = 0.6f;
        [SerializeField, Range(1, 32)] private int maxRhythmPulses = 8;
        [SerializeField, Range(0.1f, 4f)] private float rhythmPulseLifetime = 1.2f;
        [SerializeField, Range(0.01f, 1f)] private float rhythmPulseRadius = 0.65f;
        [SerializeField, Range(0f, 1f)] private float rhythmPulseDisplacement = 0.08f;
        [SerializeField, Range(0f, 0.2f)] private float rhythmBreathDisplacement = 0.025f;
        [SerializeField, Range(0f, 1f)] private float ibiPhaseSpread = 0.18f;
        [SerializeField, Range(0f, 1f)] private float ibiBreakAmount = 0.7f;

        [Header("Affect Layer")]
        [SerializeField, Range(0f, 3f)] private float arousalTurbulenceMin = 0.04f;
        [SerializeField, Range(0f, 3f)] private float arousalTurbulenceMax = 0.4f;
        [SerializeField, Range(0.01f, 5f)] private float arousalMotionSpeedMin = 0.2f;
        [SerializeField, Range(0.01f, 5f)] private float arousalMotionSpeedMax = 2.5f;
        [SerializeField, Range(0.1f, 4f)] private float arousalMorphSmoothMin = 0.3f;
        [SerializeField, Range(0.1f, 4f)] private float arousalMorphSmoothMax = 1.8f;
        [SerializeField, Range(0f, 1f)] private float lowEngagementAlpha = 0.28f;
        [SerializeField, Range(0f, 1f)] private float highEngagementAlpha = 1f;
        [SerializeField, Range(0f, 3f)] private float lowEngagementParticleScale = 0.7f;
        [SerializeField, Range(0f, 3f)] private float highEngagementParticleScale = 1.35f;
        [SerializeField, Range(0f, 1f)] private float engagementMeshSurfaceStrength = 1f;

        [Header("Engagement Rigidity")]
        [SerializeField, Range(0f, 1f)] private float lowEngagementDriftRadius = 0.28f;
        [SerializeField, Range(0f, 5f)] private float lowEngagementDriftSpeed = 0.65f;
        [SerializeField, Range(0.1f, 12f)] private float lowEngagementDriftNoiseScale = 3.2f;
        [SerializeField, Range(0f, 1f)] private float lowEngagementVerticalDrift = 0.14f;
        [SerializeField, Range(0f, 1f)] private float lowEngagementRadialDrift = 0.35f;
        [SerializeField, Range(0.25f, 4f)] private float engagementRigidityPower = 1.6f;

        [Header("Engagement Lattice")]
        [SerializeField] private bool renderEngagementLattice;
        [SerializeField] private Transform engagementLatticeRoot;
        [SerializeField] private Material engagementLatticeMaterial;
        [SerializeField, Range(0f, 1f)] private float engagementLatticeThreshold = 0.55f;
        [SerializeField, Range(0.01f, 1f)] private float engagementLatticeFadeRange = 0.25f;
        [SerializeField, Range(1, 12)] private int engagementLatticeStride = 2;
        [SerializeField, Range(0.0005f, 0.02f)] private float engagementLatticeLineWidth = 0.006f;
        [SerializeField, Range(0f, 1f)] private float engagementLatticeMinimumWidthFactor = 0.25f;
        [SerializeField, Range(0.25f, 4f)] private float engagementLatticeResponsePower = 1.75f;
        [SerializeField, Range(0f, 0.08f)] private float engagementLatticeLift = 0.01f;
        [SerializeField, Range(0f, 1f)] private float engagementLatticeMaxAlpha = 0.55f;
        [SerializeField, Range(0f, 1f)] private float engagementLatticeValenceTint = 0.35f;

        [Header("Arousal Trails")]
        [SerializeField] private Transform arousalTrailRoot;
        [SerializeField] private Material arousalTrailMaterial;
        [SerializeField, Range(0f, 1f)] private float arousalTrailThreshold = 0.55f;
        [SerializeField, Range(0.01f, 1f)] private float arousalTrailFadeRange = 0.25f;
        [SerializeField, Range(1, 12)] private int arousalTrailStride = 4;
        [SerializeField, Range(4, 96)] private int maxArousalTrails = 64;
        [SerializeField, Range(3, 48)] private int arousalTrailHistoryLength = 18;
        [SerializeField, Range(0.001f, 0.08f)] private float arousalTrailLineWidth = 0.014f;
        [SerializeField, Range(0f, 0.1f)] private float arousalTrailLift = 0.018f;
        [SerializeField, Range(0f, 1f)] private float arousalTrailMaxAlpha = 0.42f;
        [SerializeField, Range(0f, 1f)] private float arousalTrailValenceTint = 0.15f;

        [Header("Particles")]
        [SerializeField] private bool renderParticles = true;
        [SerializeField, Range(0.001f, 0.12f)] private float particleSize = 0.025f;

        [Header("Colour")]
        [SerializeField] private Color lowValenceColor = new Color(1f, 0.12f, 0.08f, 0.9f);
        [SerializeField] private Color neutralColor = new Color(0.2f, 1f, 0.75f, 0.9f);
        [SerializeField] private Color highValenceColor = new Color(1f, 0.82f, 0.16f, 0.9f);
        [SerializeField] private Color hotTemperatureTrendColor = new Color(1f, 0.28f, 0.04f, 0.95f);
        [SerializeField] private Color coldTemperatureTrendColor = new Color(0.1f, 0.66f, 1f, 0.95f);
        [SerializeField, Range(1, 16)] private int maxTemperatureTrendWaves = 8;
        [SerializeField, Range(1, 8)] private int minTemperatureTrendBurstWaves = 3;
        [SerializeField, Range(1, 8)] private int maxTemperatureTrendBurstWaves = 4;
        [SerializeField, Range(0f, 1f)] private float temperatureTrendWaveThreshold = 0.18f;
        [SerializeField, Range(0.05f, 1f)] private float temperatureTrendWaveReleaseFactor = 0.65f;
        [SerializeField, Range(0.1f, 4f)] private float temperatureTrendWaveLifetime = 1.25f;
        [SerializeField, Range(0.03f, 0.8f)] private float temperatureTrendWaveWidth = 0.24f;
        [SerializeField, Range(0.05f, 1f)] private float temperatureTrendWaveInterval = 0.26f;
        [SerializeField, Range(0f, 1f)] private float temperatureTrendWaveTintStrength = 0.78f;

        [Header("Diagnostics")]
        [SerializeField] private bool logReceivedSamples = true;
        [SerializeField, Range(0f, 5f)] private float minimumLogIntervalSeconds = 0.5f;

        private MeshFilter _meshFilter;
        private ParticleSystem _particleSystem;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector3[] _velocities;
        private Color[] _colors;
        private int[] _triangles;
        private ParticleSystem.Particle[] _particles;
        private float[] _uValues;
        private float[] _vValues;
        private Vector3[] _engagementDriftDirections;
        private Vector2[] _engagementDriftSeeds;

        private struct ScrEvent
        {
            public bool Active;
            public Vector2 CenterUv;
            public float Age;
            public float Lifetime;
            public float Radius;
            public float Intensity;
            public float Direction;
        }

        private struct ScrSpark
        {
            public bool Active;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Lifetime;
            public float Intensity;
            public float Size;
        }

        private struct RhythmPulse
        {
            public bool Active;
            public float Age;
            public float Lifetime;
            public float Radius;
            public float Intensity;
            public float PhaseSeed;
            public float Direction;
        }

        private struct TemperatureTrendWave
        {
            public bool Active;
            public float Age;
            public float Lifetime;
            public float Intensity;
            public float Direction;
            public float Width;
        }

        private ScrEvent[] _scrEvents;
        private float _scrEventAccumulator;
        private int _nextScrEventIndex;
        private float _previousScrFrequency;

        private ScrSpark[] _scrSparks;
        private ParticleSystem.Particle[] _scrSparkParticles;
        private int _nextScrSparkIndex;

        private RhythmPulse[] _rhythmPulses;
        private float _rhythmPhase;
        private int _nextRhythmPulseIndex;

        private TemperatureTrendWave[] _temperatureTrendWaves;
        private int _nextTemperatureTrendWaveIndex;
        private int _temperatureTrendBurstRemaining;
        private float _temperatureTrendBurstMagnitude;
        private float _temperatureTrendWaveSpawnTimer;
        private float _temperatureTrendActiveDirection;

        private LineRenderer[] _engagementLatticeLines;
        private int[] _engagementLatticeXCoordinates;
        private int[] _engagementLatticeYCoordinates;
        private Material _engagementLatticeMaterialInstance;
        private int _builtLatticeGridWidth;
        private int _builtLatticeGridHeight;
        private int _builtLatticeStride;

        private LineRenderer[] _arousalTrailLines;
        private int[] _arousalTrailVertexIndices;
        private Vector3[][] _arousalTrailHistory;
        private Material _arousalTrailMaterialInstance;
        private int _arousalTrailHead;
        private bool _arousalTrailHistoryFilled;
        private int _builtArousalTrailGridWidth;
        private int _builtArousalTrailGridHeight;
        private int _builtArousalTrailStride;
        private int _builtArousalTrailMaxCount;
        private int _builtArousalTrailHistoryLength;

        private int _builtGridWidth;
        private int _builtGridHeight;
        private bool _needsRebuild = true;
        private float _localTime;

        private float _tonicEda;
        private float _temperatureRate;
        private float _scrFrequency;
        private float _heartRate;
        private float _interBeatInterval;
        private float _facialArousal = 0.5f;
        private float _facialValence = 0.5f;
        private float _engagement = 0.5f;

        private float _targetTonicEda;
        private float _targetTemperatureRate;
        private float _targetScrFrequency;
        private float _targetHeartRate;
        private float _targetInterBeatInterval;
        private float _targetFacialArousal = 0.5f;
        private float _targetFacialValence = 0.5f;
        private float _targetEngagement = 0.5f;

        private double _nextLogTime;

        private void Awake()
        {
            ResolveComponents();
            ResolveManualDriver();
            EnsureInitialized();
        }

        private void OnEnable()
        {
            ResolveComponents();
            ResolveManualDriver();
            EnsureInitialized();

            if (manualDriver == null)
            {
                return;
            }

            manualDriver.OnFrame += OnManualDriverFrame;

            if (manualDriver.HasLatestFrame)
            {
                OnManualDriverFrame(manualDriver.LatestFrame);
            }
        }

        private void Update()
        {
            EnsureInitialized();
            EnsureScrEventCapacity();
            EnsureScrSparkCapacity();
            EnsureScrSparkParticleSystem();
            EnsureRhythmPulseCapacity();
            EnsureTemperatureTrendWaveCapacity();

            float deltaTime = ResolveDeltaTime();
            _localTime += deltaTime * internalTimeScale;

            SmoothSignals(deltaTime);
            UpdateScrEvents(deltaTime);
            UpdateRhythmPulses(deltaTime);
            UpdateTemperatureTrendWaves(deltaTime);
            UpdateScrSparks(deltaTime);
            UpdateGeometry(deltaTime);
            if (renderEngagementLattice)
            {
                UpdateEngagementLattice();
            }
            else
            {
                ClearEngagementLattice();
            }
            UpdateArousalTrails();
            ApplyScrSparkParticles();
        }

        private void OnDisable()
        {
            if (manualDriver != null)
            {
                manualDriver.OnFrame -= OnManualDriverFrame;
            }

            ClearScrSparks();
            ClearTemperatureTrendWaves();
            ClearEngagementLattice();
            ClearArousalTrails(true);
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(_mesh);
                }
                else
                {
                    DestroyImmediate(_mesh);
                }
            }

            if (_engagementLatticeMaterialInstance != null)
            {
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(_engagementLatticeMaterialInstance);
                }
                else
                {
                    DestroyImmediate(_engagementLatticeMaterialInstance);
                }
            }

            if (_arousalTrailMaterialInstance != null)
            {
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(_arousalTrailMaterialInstance);
                }
                else
                {
                    DestroyImmediate(_arousalTrailMaterialInstance);
                }
            }
        }

        private void OnValidate()
        {
            gridWidth = Mathf.Max(4, gridWidth);
            gridHeight = Mathf.Max(4, gridHeight);
            engagementLatticeLineWidth = Mathf.Clamp(engagementLatticeLineWidth, 0.0005f, 0.02f);
            engagementLatticeMinimumWidthFactor = Mathf.Clamp01(engagementLatticeMinimumWidthFactor);
            engagementLatticeResponsePower = Mathf.Clamp(engagementLatticeResponsePower, 0.25f, 4f);
            lowEngagementDriftRadius = Mathf.Clamp01(lowEngagementDriftRadius);
            lowEngagementDriftNoiseScale = Mathf.Clamp(lowEngagementDriftNoiseScale, 0.1f, 12f);
            lowEngagementRadialDrift = Mathf.Clamp01(lowEngagementRadialDrift);
            engagementRigidityPower = Mathf.Clamp(engagementRigidityPower, 0.25f, 4f);
            maxTemperatureTrendWaves = Mathf.Max(1, maxTemperatureTrendWaves);
            minTemperatureTrendBurstWaves = Mathf.Clamp(minTemperatureTrendBurstWaves, 1, maxTemperatureTrendWaves);
            maxTemperatureTrendBurstWaves = Mathf.Clamp(maxTemperatureTrendBurstWaves, 1, maxTemperatureTrendWaves);
            maxTemperatureTrendBurstWaves = Mathf.Max(minTemperatureTrendBurstWaves, maxTemperatureTrendBurstWaves);
            temperatureTrendWaveReleaseFactor = Mathf.Clamp(temperatureTrendWaveReleaseFactor, 0.05f, 1f);
            temperatureTrendWaveThreshold = Mathf.Clamp01(temperatureTrendWaveThreshold);
            temperatureTrendWaveLifetime = Mathf.Max(0.1f, temperatureTrendWaveLifetime);
            temperatureTrendWaveWidth = Mathf.Clamp(temperatureTrendWaveWidth, 0.03f, 0.8f);
            temperatureTrendWaveInterval = Mathf.Clamp(temperatureTrendWaveInterval, 0.05f, 1f);
            temperatureTrendWaveTintStrength = Mathf.Clamp01(temperatureTrendWaveTintStrength);
            _needsRebuild = true;

            if (isActiveAndEnabled)
            {
                ResolveComponents();
                EnsureInitialized();
                EnsureScrEventCapacity();
                EnsureScrSparkCapacity();
                EnsureScrSparkParticleSystem();
                EnsureRhythmPulseCapacity();
                EnsureTemperatureTrendWaveCapacity();
                EnsureEngagementLatticeCapacity();
                EnsureArousalTrailCapacity();
            }
        }

        public void Apply(in ParticleMeshSignalSample parameters, long timestampTicksUtc)
        {
            ReceiveSample(parameters, timestampTicksUtc, -1);
        }

        private void OnManualDriverFrame(DataFrame<ParticleMeshSignalSample> frame)
        {
            ReceiveSample(frame.Payload, frame.TimestampTicksUtc, frame.SequenceId);
        }

        private void ResolveComponents()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _particleSystem = GetComponent<ParticleSystem>();

            if (scrSparkParticleSystem == null)
            {
                ParticleSystem fallbackParticleSystem = null;
                ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particleSystems.Length; i++)
                {
                    ParticleSystem candidate = particleSystems[i];
                    if (candidate == null || candidate == _particleSystem)
                    {
                        continue;
                    }

                    fallbackParticleSystem ??= candidate;
                    string candidateName = candidate.name;
                    if (candidateName.IndexOf("scr", System.StringComparison.OrdinalIgnoreCase) >= 0
                        && candidateName.IndexOf("spark", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        scrSparkParticleSystem = candidate;
                        break;
                    }
                }

                scrSparkParticleSystem ??= fallbackParticleSystem;
            }

            if (engagementLatticeRoot == null)
            {
                Transform existingLattice = transform.Find("Engagement Lattice");
                if (existingLattice != null)
                {
                    engagementLatticeRoot = existingLattice;
                }
            }

            if (arousalTrailRoot == null)
            {
                Transform existingTrails = transform.Find("Arousal Trails");
                if (existingTrails != null)
                {
                    arousalTrailRoot = existingTrails;
                }
            }
        }

        private void ResolveManualDriver()
        {
            if (manualDriver == null)
            {
                manualDriver = GetComponent<ParticleMeshManualDriver>();
            }
        }

        private void EnsureInitialized()
        {
            if (!_needsRebuild && _mesh != null && _builtGridWidth == gridWidth && _builtGridHeight == gridHeight)
            {
                return;
            }

            int width = Mathf.Max(4, gridWidth);
            int height = Mathf.Max(4, gridHeight);
            int vertexCount = width * height;

            if (_mesh == null)
            {
                _mesh = new Mesh
                {
                    name = "ParticleMesh Generated Mesh",
                    hideFlags = HideFlags.DontSave
                };
                _mesh.MarkDynamic();
            }
            else
            {
                _mesh.Clear();
            }

            _vertices = new Vector3[vertexCount];
            _velocities = new Vector3[vertexCount];
            _colors = new Color[vertexCount];
            _particles = new ParticleSystem.Particle[vertexCount];
            _uValues = new float[vertexCount];
            _vValues = new float[vertexCount];
            _engagementDriftDirections = new Vector3[vertexCount];
            _engagementDriftSeeds = new Vector2[vertexCount];
            _triangles = BuildGridTriangles(width, height);

            for (int y = 0; y < height; y++)
            {
                float v = height == 1 ? 0f : (float)y / (height - 1);
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    float u = width == 1 ? 0f : (float)x / (width - 1);
                    _uValues[index] = u;
                    _vValues[index] = v;
                    _engagementDriftDirections[index] = BuildEngagementDriftDirection(index, u, v);
                    _engagementDriftSeeds[index] = new Vector2(
                        BuildStableUnit(index, 23.17f) * 19.31f,
                        BuildStableUnit(index, 61.83f) * 29.47f);
                    _vertices[index] = new Vector3((u - 0.5f) * visualScale, 0f, (v - 0.5f) * visualScale);
                    _colors[index] = neutralColor;
                    _particles[index].position = _vertices[index];
                    _particles[index].startColor = neutralColor;
                    _particles[index].startSize = particleSize;
                    _particles[index].startLifetime = float.MaxValue;
                    _particles[index].remainingLifetime = float.MaxValue;
                }
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            if (_meshFilter != null)
            {
                _meshFilter.sharedMesh = _mesh;
            }

            ConfigureParticleSystem(vertexCount);
            EnsureScrEventCapacity();
            EnsureScrSparkCapacity();
            EnsureScrSparkParticleSystem();
            EnsureRhythmPulseCapacity();
            EnsureTemperatureTrendWaveCapacity();
            EnsureEngagementLatticeCapacity();
            EnsureArousalTrailCapacity();
            _builtGridWidth = width;
            _builtGridHeight = height;
            _needsRebuild = false;
        }

        private void EnsureScrEventCapacity()
        {
            int eventCount = Mathf.Max(1, maxScrEvents);
            if (_scrEvents != null && _scrEvents.Length == eventCount)
            {
                return;
            }

            _scrEvents = new ScrEvent[eventCount];
            _scrEventAccumulator = 0f;
            _nextScrEventIndex = 0;
            _previousScrFrequency = _scrFrequency;
        }

        private void EnsureScrSparkCapacity()
        {
            int sparkCount = Mathf.Max(0, maxScrSparks);
            if (_scrSparks != null && _scrSparks.Length == sparkCount)
            {
                return;
            }

            _scrSparks = new ScrSpark[sparkCount];
            _scrSparkParticles = new ParticleSystem.Particle[sparkCount];
            _nextScrSparkIndex = 0;
            if (sparkCount == 0)
            {
                ClearScrSparks();
            }
        }

        private void EnsureScrSparkParticleSystem()
        {
            if (scrSparkParticleSystem == null && UnityEngine.Application.isPlaying)
            {
                var sparkObject = new GameObject("SCR Spark Particles");
                sparkObject.transform.SetParent(transform, false);
                scrSparkParticleSystem = sparkObject.AddComponent<ParticleSystem>();
            }

            ConfigureScrSparkParticleSystem();
        }

        private void ConfigureScrSparkParticleSystem()
        {
            if (scrSparkParticleSystem == null)
            {
                return;
            }

            var main = scrSparkParticleSystem.main;
            main.maxParticles = Mathf.Max(1, maxScrSparks);
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = float.MaxValue;
            main.startSpeed = 0f;
            main.startSize = scrSparkSize;

            var emission = scrSparkParticleSystem.emission;
            emission.enabled = false;

            var shape = scrSparkParticleSystem.shape;
            shape.enabled = false;

            var renderer = scrSparkParticleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingFudge = 1f;
            }
        }

        private void UpdateScrEvents(float deltaTime)
        {
            EnsureScrEventCapacity();

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            for (int i = 0; i < _scrEvents.Length; i++)
            {
                if (!_scrEvents[i].Active)
                {
                    continue;
                }

                _scrEvents[i].Age += safeDeltaTime;
                if (_scrEvents[i].Age >= _scrEvents[i].Lifetime)
                {
                    _scrEvents[i].Active = false;
                }
            }

            float scrMagnitude = Mathf.Abs(_scrFrequency);
            float previousScrMagnitude = Mathf.Abs(_previousScrFrequency);
            float sustainedDrive = Mathf.InverseLerp(scrEventThreshold, 1f, scrMagnitude);
            float riseDrive = Mathf.Max(0f, scrMagnitude - previousScrMagnitude) * Mathf.Max(0f, scrRiseEventGain);
            _scrEventAccumulator += sustainedDrive * Mathf.Max(0f, scrEventsPerSecondAtMax) * safeDeltaTime;
            _scrEventAccumulator += riseDrive;

            int spawnedThisFrame = 0;
            while (_scrEventAccumulator >= 1f && spawnedThisFrame < 4)
            {
                float direction = _scrFrequency < 0f ? -1f : 1f;
                SpawnScrEvent(direction * Mathf.Clamp01(Mathf.Max(scrMagnitude, sustainedDrive)));
                _scrEventAccumulator -= 1f;
                spawnedThisFrame++;
            }

            if (_scrEventAccumulator > 4f)
            {
                _scrEventAccumulator = 4f;
            }

            _previousScrFrequency = _scrFrequency;
        }

        private void SpawnScrEvent(float signedIntensity)
        {
            if (_scrEvents == null || _scrEvents.Length == 0)
            {
                return;
            }

            int eventIndex = _nextScrEventIndex % _scrEvents.Length;
            float placementIndex = _nextScrEventIndex;
            _nextScrEventIndex++;

            float u = Mathf.Repeat(placementIndex * 0.6180339f + _localTime * 0.037f, 1f);
            float v = Mathf.Repeat(placementIndex * 0.381966f + Mathf.Sin(_localTime * 0.11f + placementIndex) * 0.17f, 1f);
            float direction = signedIntensity < 0f ? -1f : 1f;
            float clampedIntensity = Mathf.Clamp01(Mathf.Abs(signedIntensity));

            _scrEvents[eventIndex] = new ScrEvent
            {
                Active = true,
                CenterUv = new Vector2(u, v),
                Age = 0f,
                Lifetime = Mathf.Max(0.1f, scrEventLifetime) * Mathf.Lerp(0.75f, 1.35f, clampedIntensity),
                Radius = Mathf.Max(0.01f, scrEventRadius) * Mathf.Lerp(0.75f, 1.4f, clampedIntensity),
                Intensity = Mathf.Lerp(0.35f, 1f, clampedIntensity),
                Direction = direction
            };

            SpawnScrSparks(new Vector2(u, v), direction * clampedIntensity);
        }

        private void SpawnScrSparks(Vector2 centerUv, float signedIntensity)
        {
            EnsureScrSparkCapacity();
            if (_scrSparks == null || _scrSparks.Length == 0 || scrSparksPerEventAtMax <= 0)
            {
                return;
            }

            float direction = signedIntensity < 0f ? -1f : 1f;
            float clampedIntensity = Mathf.Clamp01(Mathf.Abs(signedIntensity));
            int sparkCount = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(1f, scrSparksPerEventAtMax, clampedIntensity)),
                0,
                _scrSparks.Length);
            if (sparkCount <= 0)
            {
                return;
            }

            Vector3 origin = SampleMeshLocalPosition(centerUv);
            float lifetime = Mathf.Max(0.05f, scrSparkLifetime) * Mathf.Lerp(0.75f, 1.25f, clampedIntensity);
            float spread = scrSparkSpread * Mathf.Lerp(0.55f, 1.35f, clampedIntensity);
            float lift = scrSparkLift * Mathf.Lerp(0.65f, 1.35f, clampedIntensity);
            for (int i = 0; i < sparkCount; i++)
            {
                int sparkIndex = _nextScrSparkIndex % _scrSparks.Length;
                float seed = (_nextScrSparkIndex + 1) * 1.6180339f;
                _nextScrSparkIndex++;

                float angle = seed * Tau;
                float radius = spread * Mathf.Lerp(0.25f, 1f, Mathf.Repeat(seed * 0.7548777f, 1f));
                Vector3 lateral = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Vector3 velocity = direction > 0f
                    ? (lateral / lifetime) + (Vector3.up * lift)
                    : (-lateral / lifetime) + (Vector3.down * lift);
                _scrSparks[sparkIndex] = new ScrSpark
                {
                    Active = true,
                    Position = origin + lateral * 0.2f,
                    Velocity = velocity,
                    Age = 0f,
                    Lifetime = lifetime,
                    Intensity = Mathf.Lerp(0.35f, 1f, clampedIntensity),
                    Size = scrSparkSize * Mathf.Lerp(0.75f, 1.8f, clampedIntensity)
                };
            }
        }

        private void UpdateScrSparks(float deltaTime)
        {
            if (_scrSparks == null || _scrSparks.Length == 0)
            {
                return;
            }

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            for (int i = 0; i < _scrSparks.Length; i++)
            {
                if (!_scrSparks[i].Active)
                {
                    continue;
                }

                _scrSparks[i].Age += safeDeltaTime;
                if (_scrSparks[i].Age >= _scrSparks[i].Lifetime)
                {
                    _scrSparks[i].Active = false;
                    continue;
                }

                _scrSparks[i].Position += _scrSparks[i].Velocity * safeDeltaTime;
                _scrSparks[i].Velocity = Vector3.Lerp(_scrSparks[i].Velocity, Vector3.zero, safeDeltaTime * 1.8f);
            }
        }

        private void ApplyScrSparkParticles()
        {
            if (scrSparkParticleSystem == null || _scrSparks == null || _scrSparkParticles == null)
            {
                return;
            }

            int particleCount = 0;
            Color sparkTint = Color.Lerp(ResolveBaseColor(_facialValence), scrSparkColor, 0.78f);
            for (int i = 0; i < _scrSparks.Length; i++)
            {
                if (!_scrSparks[i].Active)
                {
                    continue;
                }

                float normalizedAge = Mathf.Clamp01(_scrSparks[i].Age / Mathf.Max(0.0001f, _scrSparks[i].Lifetime));
                float fade = 1f - normalizedAge;
                Color particleColor = sparkTint;
                particleColor.a *= fade * _scrSparks[i].Intensity;

                _scrSparkParticles[particleCount].position = _scrSparks[i].Position;
                _scrSparkParticles[particleCount].startColor = particleColor;
                _scrSparkParticles[particleCount].startSize = _scrSparks[i].Size * Mathf.Lerp(0.35f, 1f, fade);
                _scrSparkParticles[particleCount].startLifetime = float.MaxValue;
                _scrSparkParticles[particleCount].remainingLifetime = float.MaxValue;
                particleCount++;
            }

            if (particleCount > 0)
            {
                scrSparkParticleSystem.SetParticles(_scrSparkParticles, particleCount);
            }
            else
            {
                scrSparkParticleSystem.Clear();
            }
        }

        private void ClearScrSparks()
        {
            if (_scrSparks != null)
            {
                for (int i = 0; i < _scrSparks.Length; i++)
                {
                    _scrSparks[i].Active = false;
                }
            }

            if (scrSparkParticleSystem != null)
            {
                scrSparkParticleSystem.Clear();
            }
        }

        private Vector3 SampleMeshLocalPosition(Vector2 uv)
        {
            if (_vertices == null || _vertices.Length == 0 || _builtGridWidth < 2 || _builtGridHeight < 2)
            {
                return new Vector3((uv.x - 0.5f) * visualScale, 0f, (uv.y - 0.5f) * visualScale);
            }

            float x = Mathf.Clamp01(uv.x) * (_builtGridWidth - 1);
            float y = Mathf.Clamp01(uv.y) * (_builtGridHeight - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, _builtGridWidth - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, _builtGridHeight - 1);
            int x1 = Mathf.Min(x0 + 1, _builtGridWidth - 1);
            int y1 = Mathf.Min(y0 + 1, _builtGridHeight - 1);
            float xBlend = x - x0;
            float yBlend = y - y0;

            Vector3 lower = Vector3.Lerp(_vertices[y0 * _builtGridWidth + x0], _vertices[y0 * _builtGridWidth + x1], xBlend);
            Vector3 upper = Vector3.Lerp(_vertices[y1 * _builtGridWidth + x0], _vertices[y1 * _builtGridWidth + x1], xBlend);
            return Vector3.Lerp(lower, upper, yBlend);
        }

        private void EnsureRhythmPulseCapacity()
        {
            int pulseCount = Mathf.Max(1, maxRhythmPulses);
            if (_rhythmPulses != null && _rhythmPulses.Length == pulseCount)
            {
                return;
            }

            _rhythmPulses = new RhythmPulse[pulseCount];
            _rhythmPhase = 0f;
            _nextRhythmPulseIndex = 0;
        }

        private void UpdateRhythmPulses(float deltaTime)
        {
            EnsureRhythmPulseCapacity();

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            for (int i = 0; i < _rhythmPulses.Length; i++)
            {
                if (!_rhythmPulses[i].Active)
                {
                    continue;
                }

                _rhythmPulses[i].Age += safeDeltaTime;
                if (_rhythmPulses[i].Age >= _rhythmPulses[i].Lifetime)
                {
                    _rhythmPulses[i].Active = false;
                }
            }

            float baseHz = Mathf.Max(20f, rhythmBaseBpm) / 60f;
            float rateMultiplier = Mathf.Clamp(1f + _heartRate * Mathf.Max(0f, rhythmRateStdDevBoost), 0.25f, 3f);
            _rhythmPhase += baseHz * rateMultiplier * safeDeltaTime;

            int spawnedThisFrame = 0;
            while (_rhythmPhase >= 1f && spawnedThisFrame < 4)
            {
                SpawnRhythmPulse(_heartRate);
                _rhythmPhase -= 1f;
                spawnedThisFrame++;
            }

            if (_rhythmPhase > 4f)
            {
                _rhythmPhase = Mathf.Repeat(_rhythmPhase, 1f);
            }
        }

        private void SpawnRhythmPulse(float signedIntensity)
        {
            if (_rhythmPulses == null || _rhythmPulses.Length == 0)
            {
                return;
            }

            int pulseIndex = _nextRhythmPulseIndex % _rhythmPulses.Length;
            float placementIndex = _nextRhythmPulseIndex;
            _nextRhythmPulseIndex++;

            float direction = signedIntensity < 0f ? -1f : 1f;
            float clampedIntensity = Mathf.Clamp01(Mathf.Abs(signedIntensity));
            _rhythmPulses[pulseIndex] = new RhythmPulse
            {
                Active = true,
                Age = 0f,
                Lifetime = Mathf.Max(0.1f, rhythmPulseLifetime) * Mathf.Lerp(1.15f, 0.85f, clampedIntensity),
                Radius = Mathf.Max(0.01f, rhythmPulseRadius) * Mathf.Lerp(0.85f, 1.25f, clampedIntensity),
                Intensity = Mathf.Lerp(0.35f, 1f, clampedIntensity),
                PhaseSeed = placementIndex * 1.6180339f,
                Direction = direction
            };
        }

        private void EnsureTemperatureTrendWaveCapacity()
        {
            int waveCount = Mathf.Max(1, maxTemperatureTrendWaves);
            if (_temperatureTrendWaves != null && _temperatureTrendWaves.Length == waveCount)
            {
                return;
            }

            _temperatureTrendWaves = new TemperatureTrendWave[waveCount];
            _nextTemperatureTrendWaveIndex = 0;
            _temperatureTrendBurstRemaining = 0;
            _temperatureTrendBurstMagnitude = 0f;
            _temperatureTrendWaveSpawnTimer = 0f;
            _temperatureTrendActiveDirection = 0f;
        }

        private void UpdateTemperatureTrendWaves(float deltaTime)
        {
            EnsureTemperatureTrendWaveCapacity();

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            for (int i = 0; i < _temperatureTrendWaves.Length; i++)
            {
                if (!_temperatureTrendWaves[i].Active)
                {
                    continue;
                }

                _temperatureTrendWaves[i].Age += safeDeltaTime;
                if (_temperatureTrendWaves[i].Age >= _temperatureTrendWaves[i].Lifetime)
                {
                    _temperatureTrendWaves[i].Active = false;
                }
            }

            float magnitude = Mathf.Abs(_temperatureRate);
            float threshold = Mathf.Max(0.0001f, temperatureTrendWaveThreshold);
            float releaseThreshold = threshold * Mathf.Clamp(temperatureTrendWaveReleaseFactor, 0.05f, 1f);
            if (magnitude >= threshold)
            {
                float direction = _temperatureRate < 0f ? -1f : 1f;
                if (_temperatureTrendActiveDirection == 0f || Mathf.Sign(_temperatureTrendActiveDirection) != direction)
                {
                    StartTemperatureTrendBurst(direction, magnitude);
                }
            }
            else if (magnitude <= releaseThreshold && _temperatureTrendBurstRemaining == 0)
            {
                _temperatureTrendActiveDirection = 0f;
                _temperatureTrendBurstMagnitude = 0f;
                _temperatureTrendWaveSpawnTimer = 0f;
            }

            if (_temperatureTrendBurstRemaining <= 0 || _temperatureTrendActiveDirection == 0f)
            {
                return;
            }

            _temperatureTrendWaveSpawnTimer -= safeDeltaTime;
            int spawnedThisFrame = 0;
            while (_temperatureTrendWaveSpawnTimer <= 0f && _temperatureTrendBurstRemaining > 0 && spawnedThisFrame < 4)
            {
                SpawnTemperatureTrendWave(_temperatureTrendActiveDirection * _temperatureTrendBurstMagnitude);
                _temperatureTrendBurstRemaining--;
                spawnedThisFrame++;

                float drive = Mathf.InverseLerp(threshold, 1f, _temperatureTrendBurstMagnitude);
                float interval = Mathf.Max(0.05f, temperatureTrendWaveInterval) * Mathf.Lerp(1.25f, 0.65f, drive);
                _temperatureTrendWaveSpawnTimer += interval;
            }
        }

        private void StartTemperatureTrendBurst(float direction, float magnitude)
        {
            float threshold = Mathf.Max(0.0001f, temperatureTrendWaveThreshold);
            float clampedMagnitude = Mathf.Clamp01(magnitude);
            float drive = Mathf.InverseLerp(threshold, 1f, clampedMagnitude);
            int maxBurstCount = Mathf.Clamp(maxTemperatureTrendBurstWaves, 1, Mathf.Max(1, maxTemperatureTrendWaves));
            int minBurstCount = Mathf.Clamp(minTemperatureTrendBurstWaves, 1, maxBurstCount);
            int burstCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(minBurstCount, maxBurstCount, drive)), minBurstCount, maxBurstCount);

            _temperatureTrendActiveDirection = direction < 0f ? -1f : 1f;
            _temperatureTrendBurstMagnitude = clampedMagnitude;
            _temperatureTrendBurstRemaining = burstCount;
            _temperatureTrendWaveSpawnTimer = Mathf.Min(_temperatureTrendWaveSpawnTimer, 0f);
        }

        private void SpawnTemperatureTrendWave(float signedIntensity)
        {
            if (_temperatureTrendWaves == null || _temperatureTrendWaves.Length == 0)
            {
                return;
            }

            int waveIndex = _nextTemperatureTrendWaveIndex % _temperatureTrendWaves.Length;
            _nextTemperatureTrendWaveIndex++;

            float direction = signedIntensity < 0f ? -1f : 1f;
            float clampedIntensity = Mathf.Clamp01(Mathf.Abs(signedIntensity));
            _temperatureTrendWaves[waveIndex] = new TemperatureTrendWave
            {
                Active = true,
                Age = 0f,
                Lifetime = Mathf.Max(0.1f, temperatureTrendWaveLifetime) * Mathf.Lerp(0.85f, 1.25f, clampedIntensity),
                Intensity = Mathf.Lerp(0.35f, 1f, clampedIntensity),
                Direction = direction,
                Width = Mathf.Max(0.03f, temperatureTrendWaveWidth) * Mathf.Lerp(0.75f, 1.25f, clampedIntensity)
            };
        }

        private void ClearTemperatureTrendWaves()
        {
            if (_temperatureTrendWaves != null)
            {
                for (int i = 0; i < _temperatureTrendWaves.Length; i++)
                {
                    _temperatureTrendWaves[i].Active = false;
                }
            }

            _temperatureTrendBurstRemaining = 0;
            _temperatureTrendBurstMagnitude = 0f;
            _temperatureTrendWaveSpawnTimer = 0f;
            _temperatureTrendActiveDirection = 0f;
        }

        private void EnsureEngagementLatticeCapacity()
        {
            int width = Mathf.Max(4, gridWidth);
            int height = Mathf.Max(4, gridHeight);
            int stride = Mathf.Max(1, engagementLatticeStride);

            if (engagementLatticeRoot == null)
            {
                if (!UnityEngine.Application.isPlaying)
                {
                    return;
                }

                var latticeObject = new GameObject("Engagement Lattice");
                latticeObject.transform.SetParent(transform, false);
                engagementLatticeRoot = latticeObject.transform;
            }

            if (_engagementLatticeLines != null
                && _engagementLatticeXCoordinates != null
                && _engagementLatticeYCoordinates != null
                && _builtLatticeGridWidth == width
                && _builtLatticeGridHeight == height
                && _builtLatticeStride == stride)
            {
                return;
            }

            _engagementLatticeXCoordinates = BuildLatticeCoordinates(width, stride);
            _engagementLatticeYCoordinates = BuildLatticeCoordinates(height, stride);
            int lineCount = _engagementLatticeXCoordinates.Length + _engagementLatticeYCoordinates.Length;
            _engagementLatticeLines = new LineRenderer[lineCount];

            LineRenderer[] existingLines = engagementLatticeRoot.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lineCount; i++)
            {
                LineRenderer line = i < existingLines.Length
                    ? existingLines[i]
                    : CreateEngagementLatticeLine(i);

                int positionCount = i < _engagementLatticeYCoordinates.Length
                    ? _engagementLatticeXCoordinates.Length
                    : _engagementLatticeYCoordinates.Length;
                ConfigureEngagementLatticeLine(line, positionCount);
                _engagementLatticeLines[i] = line;
            }

            for (int i = lineCount; i < existingLines.Length; i++)
            {
                if (existingLines[i] != null)
                {
                    existingLines[i].enabled = false;
                    existingLines[i].positionCount = 0;
                }
            }

            _builtLatticeGridWidth = width;
            _builtLatticeGridHeight = height;
            _builtLatticeStride = stride;
        }

        private LineRenderer CreateEngagementLatticeLine(int lineIndex)
        {
            var lineObject = new GameObject($"Lattice Line {lineIndex:00}");
            lineObject.transform.SetParent(engagementLatticeRoot, false);
            return lineObject.AddComponent<LineRenderer>();
        }

        private void ConfigureEngagementLatticeLine(LineRenderer line, int positionCount)
        {
            if (line == null)
            {
                return;
            }

            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = positionCount;
            line.widthMultiplier = 1f;
            line.startWidth = engagementLatticeLineWidth;
            line.endWidth = engagementLatticeLineWidth;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            Material material = ResolveEngagementLatticeMaterial();
            if (material != null)
            {
                line.sharedMaterial = material;
            }
        }

        private Material ResolveEngagementLatticeMaterial()
        {
            if (engagementLatticeMaterial != null)
            {
                return engagementLatticeMaterial;
            }

            if (_engagementLatticeMaterialInstance != null)
            {
                return _engagementLatticeMaterialInstance;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            }

            if (shader == null)
            {
                return null;
            }

            _engagementLatticeMaterialInstance = new Material(shader)
            {
                name = "Engagement Lattice Runtime Material",
                hideFlags = HideFlags.DontSave
            };
            return _engagementLatticeMaterialInstance;
        }

        private void UpdateEngagementLattice()
        {
            EnsureEngagementLatticeCapacity();
            if (_vertices == null
                || _engagementLatticeLines == null
                || _engagementLatticeXCoordinates == null
                || _engagementLatticeYCoordinates == null)
            {
                return;
            }

            if (engagementLatticeMaxAlpha <= 0f)
            {
                ClearEngagementLattice();
                return;
            }

            float fadeRange = Mathf.Max(0.01f, engagementLatticeFadeRange);
            float engagementStrength = Mathf.InverseLerp(
                engagementLatticeThreshold,
                Mathf.Min(1f, engagementLatticeThreshold + fadeRange),
                Mathf.Clamp01(_engagement));

            Color latticeColor = Color.Lerp(ResolveBaseColor(_facialValence), Color.white, engagementLatticeValenceTint);
            float reactiveStrength = Mathf.Pow(engagementStrength, Mathf.Max(0.25f, engagementLatticeResponsePower));
            latticeColor.a = engagementLatticeMaxAlpha * Mathf.Lerp(0.18f, 1f, reactiveStrength);
            float lineWidth = engagementLatticeLineWidth * Mathf.Lerp(engagementLatticeMinimumWidthFactor, 1f, reactiveStrength);
            Vector3 lift = Vector3.up * engagementLatticeLift;

            int lineIndex = 0;
            for (int yIndex = 0; yIndex < _engagementLatticeYCoordinates.Length; yIndex++)
            {
                LineRenderer line = _engagementLatticeLines[lineIndex];
                if (line != null)
                {
                    line.enabled = true;
                    line.startColor = latticeColor;
                    line.endColor = latticeColor;
                    line.startWidth = lineWidth;
                    line.endWidth = lineWidth;
                    line.positionCount = _engagementLatticeXCoordinates.Length;

                    int y = _engagementLatticeYCoordinates[yIndex];
                    for (int xIndex = 0; xIndex < _engagementLatticeXCoordinates.Length; xIndex++)
                    {
                        int x = _engagementLatticeXCoordinates[xIndex];
                        line.SetPosition(xIndex, _vertices[y * _builtGridWidth + x] + lift);
                    }
                }

                lineIndex++;
            }

            for (int xIndex = 0; xIndex < _engagementLatticeXCoordinates.Length; xIndex++)
            {
                LineRenderer line = _engagementLatticeLines[lineIndex];
                if (line != null)
                {
                    line.enabled = true;
                    line.startColor = latticeColor;
                    line.endColor = latticeColor;
                    line.startWidth = lineWidth;
                    line.endWidth = lineWidth;
                    line.positionCount = _engagementLatticeYCoordinates.Length;

                    int x = _engagementLatticeXCoordinates[xIndex];
                    for (int yIndex = 0; yIndex < _engagementLatticeYCoordinates.Length; yIndex++)
                    {
                        int y = _engagementLatticeYCoordinates[yIndex];
                        line.SetPosition(yIndex, _vertices[y * _builtGridWidth + x] + lift);
                    }
                }

                lineIndex++;
            }
        }

        private void ClearEngagementLattice()
        {
            if (_engagementLatticeLines == null)
            {
                return;
            }

            for (int i = 0; i < _engagementLatticeLines.Length; i++)
            {
                if (_engagementLatticeLines[i] != null)
                {
                    _engagementLatticeLines[i].enabled = false;
                }
            }
        }

        private void EnsureArousalTrailCapacity()
        {
            int width = Mathf.Max(4, gridWidth);
            int height = Mathf.Max(4, gridHeight);
            int stride = Mathf.Max(1, arousalTrailStride);
            int maxTrailCount = Mathf.Max(1, maxArousalTrails);
            int historyLength = Mathf.Max(3, arousalTrailHistoryLength);

            if (arousalTrailRoot == null)
            {
                if (!UnityEngine.Application.isPlaying)
                {
                    return;
                }

                var trailObject = new GameObject("Arousal Trails");
                trailObject.transform.SetParent(transform, false);
                arousalTrailRoot = trailObject.transform;
            }

            if (_arousalTrailLines != null
                && _arousalTrailVertexIndices != null
                && _arousalTrailHistory != null
                && _builtArousalTrailGridWidth == width
                && _builtArousalTrailGridHeight == height
                && _builtArousalTrailStride == stride
                && _builtArousalTrailMaxCount == maxTrailCount
                && _builtArousalTrailHistoryLength == historyLength)
            {
                return;
            }

            _arousalTrailVertexIndices = BuildArousalTrailVertexIndices(width, height, stride, maxTrailCount);
            _arousalTrailHistory = new Vector3[_arousalTrailVertexIndices.Length][];
            _arousalTrailLines = new LineRenderer[_arousalTrailVertexIndices.Length];
            _arousalTrailHead = 0;
            _arousalTrailHistoryFilled = false;

            LineRenderer[] existingLines = arousalTrailRoot.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < _arousalTrailVertexIndices.Length; i++)
            {
                _arousalTrailHistory[i] = new Vector3[historyLength];
                LineRenderer line = i < existingLines.Length
                    ? existingLines[i]
                    : CreateArousalTrailLine(i);
                ConfigureArousalTrailLine(line, historyLength);
                _arousalTrailLines[i] = line;
            }

            for (int i = _arousalTrailVertexIndices.Length; i < existingLines.Length; i++)
            {
                if (existingLines[i] != null)
                {
                    existingLines[i].enabled = false;
                    existingLines[i].positionCount = 0;
                }
            }

            _builtArousalTrailGridWidth = width;
            _builtArousalTrailGridHeight = height;
            _builtArousalTrailStride = stride;
            _builtArousalTrailMaxCount = maxTrailCount;
            _builtArousalTrailHistoryLength = historyLength;
        }

        private LineRenderer CreateArousalTrailLine(int lineIndex)
        {
            var lineObject = new GameObject($"Arousal Trail {lineIndex:00}");
            lineObject.transform.SetParent(arousalTrailRoot, false);
            return lineObject.AddComponent<LineRenderer>();
        }

        private void ConfigureArousalTrailLine(LineRenderer line, int positionCount)
        {
            if (line == null)
            {
                return;
            }

            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = positionCount;
            line.widthMultiplier = 1f;
            line.startWidth = arousalTrailLineWidth;
            line.endWidth = arousalTrailLineWidth;
            line.numCapVertices = 0;
            line.numCornerVertices = 1;
            Material material = ResolveArousalTrailMaterial();
            if (material != null)
            {
                line.sharedMaterial = material;
            }
        }

        private Material ResolveArousalTrailMaterial()
        {
            if (arousalTrailMaterial != null)
            {
                return arousalTrailMaterial;
            }

            if (_arousalTrailMaterialInstance != null)
            {
                return _arousalTrailMaterialInstance;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            }

            if (shader == null)
            {
                return null;
            }

            _arousalTrailMaterialInstance = new Material(shader)
            {
                name = "Arousal Trail Runtime Material",
                hideFlags = HideFlags.DontSave
            };
            return _arousalTrailMaterialInstance;
        }

        private void UpdateArousalTrails()
        {
            EnsureArousalTrailCapacity();
            if (_vertices == null
                || _arousalTrailLines == null
                || _arousalTrailVertexIndices == null
                || _arousalTrailHistory == null)
            {
                return;
            }

            float fadeRange = Mathf.Max(0.01f, arousalTrailFadeRange);
            float arousalStrength = Mathf.InverseLerp(
                arousalTrailThreshold,
                Mathf.Min(1f, arousalTrailThreshold + fadeRange),
                Mathf.Clamp01(_facialArousal));
            if (arousalStrength <= 0.001f || arousalTrailMaxAlpha <= 0f)
            {
                ClearArousalTrails(true);
                return;
            }

            Vector3 lift = Vector3.up * arousalTrailLift;
            for (int i = 0; i < _arousalTrailVertexIndices.Length; i++)
            {
                int vertexIndex = _arousalTrailVertexIndices[i];
                if (vertexIndex < 0 || vertexIndex >= _vertices.Length || _arousalTrailHistory[i] == null)
                {
                    continue;
                }

                _arousalTrailHistory[i][_arousalTrailHead] = _vertices[vertexIndex] + lift;
            }

            _arousalTrailHead++;
            if (_arousalTrailHead >= _builtArousalTrailHistoryLength)
            {
                _arousalTrailHead = 0;
                _arousalTrailHistoryFilled = true;
            }

            int availableHistory = _arousalTrailHistoryFilled ? _builtArousalTrailHistoryLength : _arousalTrailHead;
            int activeHistory = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(2f, _builtArousalTrailHistoryLength, arousalStrength)),
                2,
                Mathf.Max(2, availableHistory));
            Color headColor = Color.Lerp(ResolveBaseColor(_facialValence), highValenceColor, arousalTrailValenceTint);
            headColor.a = arousalTrailMaxAlpha * arousalStrength;
            Color tailColor = headColor;
            tailColor.a = 0f;
            float lineWidth = arousalTrailLineWidth * Mathf.Lerp(0.65f, 1.6f, arousalStrength);

            for (int lineIndex = 0; lineIndex < _arousalTrailLines.Length; lineIndex++)
            {
                LineRenderer line = _arousalTrailLines[lineIndex];
                Vector3[] history = _arousalTrailHistory[lineIndex];
                if (line == null || history == null || availableHistory < 2)
                {
                    continue;
                }

                line.enabled = true;
                line.positionCount = activeHistory;
                line.startColor = tailColor;
                line.endColor = headColor;
                line.startWidth = lineWidth * 0.25f;
                line.endWidth = lineWidth;

                int oldest = _arousalTrailHead - activeHistory;
                if (oldest < 0)
                {
                    oldest += _builtArousalTrailHistoryLength;
                }

                for (int pointIndex = 0; pointIndex < activeHistory; pointIndex++)
                {
                    int historyIndex = (oldest + pointIndex) % _builtArousalTrailHistoryLength;
                    line.SetPosition(pointIndex, history[historyIndex]);
                }
            }
        }

        private void ClearArousalTrails(bool resetHistory)
        {
            if (_arousalTrailLines != null)
            {
                for (int i = 0; i < _arousalTrailLines.Length; i++)
                {
                    if (_arousalTrailLines[i] != null)
                    {
                        _arousalTrailLines[i].enabled = false;
                    }
                }
            }

            if (!resetHistory)
            {
                return;
            }

            _arousalTrailHead = 0;
            _arousalTrailHistoryFilled = false;
        }

        private static int[] BuildArousalTrailVertexIndices(int width, int height, int stride, int maxTrailCount)
        {
            int safeWidth = Mathf.Max(1, width);
            int safeHeight = Mathf.Max(1, height);
            int safeStride = Mathf.Max(1, stride);
            int capacity = Mathf.Max(1, maxTrailCount);
            int[] indices = new int[capacity];
            int count = 0;

            for (int y = 0; y < safeHeight && count < capacity; y += safeStride)
            {
                int rowOffset = (y / safeStride) % 2 == 0 ? 0 : safeStride / 2;
                for (int x = rowOffset; x < safeWidth && count < capacity; x += safeStride)
                {
                    indices[count] = y * safeWidth + x;
                    count++;
                }
            }

            if (count == capacity)
            {
                return indices;
            }

            int[] trimmedIndices = new int[count];
            System.Array.Copy(indices, trimmedIndices, count);
            return trimmedIndices;
        }

        private static int[] BuildLatticeCoordinates(int size, int stride)
        {
            if (size <= 0)
            {
                return System.Array.Empty<int>();
            }

            int safeStride = Mathf.Max(1, stride);
            int count = 0;
            int coordinate = 0;
            while (true)
            {
                count++;
                if (coordinate >= size - 1)
                {
                    break;
                }

                coordinate = Mathf.Min(coordinate + safeStride, size - 1);
            }

            int[] coordinates = new int[count];
            coordinate = 0;
            for (int i = 0; i < count; i++)
            {
                coordinates[i] = coordinate;
                coordinate = Mathf.Min(coordinate + safeStride, size - 1);
            }

            return coordinates;
        }

        private static int[] BuildGridTriangles(int width, int height)
        {
            var triangles = new int[(width - 1) * (height - 1) * 6];
            int triangleIndex = 0;

            for (int y = 0; y < height - 1; y++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    int lowerLeft = y * width + x;
                    int lowerRight = lowerLeft + 1;
                    int upperLeft = lowerLeft + width;
                    int upperRight = upperLeft + 1;

                    triangles[triangleIndex++] = lowerLeft;
                    triangles[triangleIndex++] = upperLeft;
                    triangles[triangleIndex++] = lowerRight;
                    triangles[triangleIndex++] = lowerRight;
                    triangles[triangleIndex++] = upperLeft;
                    triangles[triangleIndex++] = upperRight;
                }
            }

            return triangles;
        }

        private void ConfigureParticleSystem(int maxParticles)
        {
            if (_particleSystem == null)
            {
                return;
            }

            var main = _particleSystem.main;
            main.maxParticles = maxParticles;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = float.MaxValue;
            main.startSpeed = 0f;
            main.startSize = particleSize;

            var emission = _particleSystem.emission;
            emission.enabled = false;

            var shape = _particleSystem.shape;
            shape.enabled = false;

            _particleSystem.Clear();
            if (renderParticles && _particles != null)
            {
                _particleSystem.SetParticles(_particles, _particles.Length);
            }
        }

        private void ReceiveSample(in ParticleMeshSignalSample sample, long timestampTicksUtc, int sequenceId)
        {
            _targetTonicEda = sample.TonicElectrodermalActivityStdDev;
            _targetTemperatureRate = sample.TemperatureRateOfChangeStdDev;
            _targetScrFrequency = sample.SkinConductanceResponseFrequencyStdDev;
            _targetHeartRate = sample.HeartRateStdDev;
            _targetInterBeatInterval = sample.InterBeatIntervalStdDev;
            _targetFacialArousal = sample.FacialEmotionArousal;
            _targetFacialValence = sample.FacialEmotionValence;
            _targetEngagement = sample.Engagement;

            if (!logReceivedSamples || !ShouldLogNow())
            {
                return;
            }

            string sequenceText = sequenceId >= 0 ? $"seq={sequenceId}" : "seq=n/a";
            Debug.Log($"{nameof(ParticleMeshVisualizer)} received {sequenceText} ts={timestampTicksUtc} {sample}", this);
        }

        private void SmoothSignals(float deltaTime)
        {
            float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, signalSmoothing) * Mathf.Max(0f, deltaTime));
            float slowFormT = 1f - Mathf.Exp(-Mathf.Max(0.01f, slowFormSmoothing) * Mathf.Max(0f, deltaTime));
            _tonicEda = Mathf.Lerp(_tonicEda, _targetTonicEda, slowFormT);
            _temperatureRate = Mathf.Lerp(_temperatureRate, _targetTemperatureRate, slowFormT);
            _scrFrequency = Mathf.Lerp(_scrFrequency, _targetScrFrequency, t);
            _heartRate = Mathf.Lerp(_heartRate, _targetHeartRate, t);
            _interBeatInterval = Mathf.Lerp(_interBeatInterval, _targetInterBeatInterval, t);
            _facialArousal = Mathf.Lerp(_facialArousal, _targetFacialArousal, t);
            _facialValence = Mathf.Lerp(_facialValence, _targetFacialValence, t);
            _engagement = Mathf.Lerp(_engagement, _targetEngagement, t);
        }

        private void UpdateGeometry(float deltaTime)
        {
            if (_mesh == null || _vertices == null || _particles == null || _engagementDriftDirections == null || _engagementDriftSeeds == null)
            {
                return;
            }

            float tonicMagnitude = Mathf.Abs(_tonicEda);
            float temperatureMagnitude = Mathf.Abs(_temperatureRate);
            float scrMagnitude = Mathf.Abs(_scrFrequency);
            float heartMagnitude = Mathf.Abs(_heartRate);
            float ibiMagnitude = Mathf.Abs(_interBeatInterval);
            float formCompression = Mathf.Lerp(1f, _tonicEda >= 0f ? 0.62f : 1.45f, tonicMagnitude);
            float formLift = Mathf.Lerp(0.02f, _tonicEda >= 0f ? 0.2f : -0.08f, tonicMagnitude);
            float contourAmplitude = Mathf.Lerp(0.025f, 0.38f, temperatureMagnitude);
            float contourFrequency = Mathf.Lerp(0.35f, 1.25f, temperatureMagnitude);
            float contourDirection = _temperatureRate < 0f ? -1f : 1f;
            float contourDrift = _localTime * contourDirection * Mathf.Lerp(0.015f, 0.12f, temperatureMagnitude);
            float arousalMotion = Mathf.Clamp01(_facialArousal);
            float engagementSolidity = Mathf.Clamp01(_engagement);
            float engagementLooseness = 1f - engagementSolidity;
            float loosenessResponse = Mathf.Pow(engagementLooseness, Mathf.Max(0.25f, engagementRigidityPower));
            float coherence = Mathf.Clamp01((engagementSolidity * meshCoherence * engagementMeshSurfaceStrength) + 0.15f);
            float arousalTurbulence = Mathf.Lerp(arousalTurbulenceMin, arousalTurbulenceMax, arousalMotion);
            float affectMotionSpeed = Mathf.Lerp(arousalMotionSpeedMin, arousalMotionSpeedMax, arousalMotion);
            float noiseAmount = turbulenceStrength * (arousalTurbulence + ibiMagnitude * 0.08f) * Mathf.Lerp(1f, 0.65f, coherence);
            float morphSmoothTime = baseMorphSmoothTime * Mathf.Lerp(arousalMorphSmoothMax, arousalMorphSmoothMin, arousalMotion);
            float engagementAlpha = Mathf.Lerp(lowEngagementAlpha, highEngagementAlpha, engagementSolidity);
            float engagementParticleScale = Mathf.Lerp(lowEngagementParticleScale, highEngagementParticleScale, engagementSolidity);
            Color valenceColor = ResolveBaseColor(_facialValence);

            for (int i = 0; i < _vertices.Length; i++)
            {
                float u = _uValues[i];
                float v = _vValues[i];
                Vector3 slowForm = BuildSlowFormTarget(u, v, formCompression, formLift, contourAmplitude, contourFrequency, contourDrift);
                Vector3 scrEventOffset = BuildScrEventOffset(u, v, out float scrEventEnergy);
                Vector3 rhythm = BuildRhythmPulseOffset(u, v, out float rhythmEnergy);
                Vector3 turbulence = BuildTurbulence(i, u, v, noiseAmount, affectMotionSpeed);
                Vector3 loosenessDrift = BuildEngagementDriftOffset(i, u, v, loosenessResponse, arousalMotion);

                Vector3 lockedTarget = (slowForm + scrEventOffset + rhythm) * visualScale;
                Vector3 target = lockedTarget + turbulence + loosenessDrift;

                _vertices[i] = Vector3.SmoothDamp(_vertices[i], target, ref _velocities[i], Mathf.Max(0.01f, morphSmoothTime), Mathf.Infinity, deltaTime);

                float localEnergy = Mathf.Clamp01(arousalMotion * 0.5f + heartMagnitude * 0.25f + scrMagnitude * 0.25f);
                Color vertexColor = Color.Lerp(valenceColor, Color.white, Mathf.Clamp01(localEnergy * 0.25f + rhythmEnergy * 0.35f + scrEventEnergy * 0.65f));
                vertexColor.a = Mathf.Lerp(engagementAlpha, highEngagementAlpha, Mathf.Max(rhythmEnergy * 0.25f, scrEventEnergy * 0.35f));
                float temperatureTrendEnergy = ResolveTemperatureTrendWaveEnergy(u, out Color temperatureTrendColor);
                if (temperatureTrendEnergy > 0f)
                {
                    float alpha = vertexColor.a;
                    vertexColor = Color.Lerp(vertexColor, temperatureTrendColor, temperatureTrendEnergy * temperatureTrendWaveTintStrength);
                    vertexColor.a = alpha;
                }

                _colors[i] = vertexColor;

                _particles[i].position = _vertices[i];
                _particles[i].startColor = vertexColor;
                _particles[i].startSize = particleSize * engagementParticleScale * Mathf.Lerp(0.75f, 1.8f, localEnergy) * Mathf.Lerp(1f, 1.8f, rhythmEnergy) * Mathf.Lerp(1f, 2.4f, scrEventEnergy);
                _particles[i].startLifetime = float.MaxValue;
                _particles[i].remainingLifetime = float.MaxValue;
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            if (_particleSystem != null)
            {
                if (renderParticles)
                {
                    _particleSystem.SetParticles(_particles, _particles.Length);
                }
                else
                {
                    _particleSystem.Clear();
                }
            }
        }

        private Vector3 BuildSlowFormTarget(
            float u,
            float v,
            float formCompression,
            float formLift,
            float contourAmplitude,
            float contourFrequency,
            float contourDrift)
        {
            float x = (u - 0.5f) * 2f;
            float z = (v - 0.5f) * 2f;
            float firstContour = Mathf.Sin((x * contourFrequency + contourDrift) * Tau);
            float secondContour = Mathf.Cos((z * contourFrequency * 0.7f - contourDrift * 0.6f) * Tau);
            float edgeDistance = Mathf.Clamp01(Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)));
            float pressureLift = formLift * (1f - edgeDistance);
            float y = ((firstContour * 0.65f) + (secondContour * 0.35f)) * contourAmplitude + pressureLift;
            return new Vector3(x * formCompression, y, z * formCompression);
        }

        private Vector3 BuildScrEventOffset(float u, float v, out float eventEnergy)
        {
            eventEnergy = 0f;
            if (_scrEvents == null || _scrEvents.Length == 0)
            {
                return Vector3.zero;
            }

            Vector2 uv = new Vector2(u, v);
            Vector3 offset = Vector3.zero;
            for (int i = 0; i < _scrEvents.Length; i++)
            {
                if (!_scrEvents[i].Active)
                {
                    continue;
                }

                float normalizedAge = Mathf.Clamp01(_scrEvents[i].Age / Mathf.Max(0.0001f, _scrEvents[i].Lifetime));
                float eventDirection = _scrEvents[i].Direction < 0f ? -1f : 1f;
                float ringRadius = _scrEvents[i].Radius * (eventDirection < 0f ? 1f - normalizedAge : normalizedAge);
                float ringWidth = Mathf.Lerp(0.025f, 0.07f, _scrEvents[i].Intensity);
                float distance = Vector2.Distance(uv, _scrEvents[i].CenterUv);
                float ring = 1f - Mathf.Clamp01(Mathf.Abs(distance - ringRadius) / ringWidth);
                float envelope = Mathf.Sin(normalizedAge * Mathf.PI) * (1f - normalizedAge * 0.35f);
                float energy = ring * envelope * _scrEvents[i].Intensity;

                if (eventDirection < 0f)
                {
                    var implosion = new Vector3(_scrEvents[i].CenterUv.x - u, 0f, _scrEvents[i].CenterUv.y - v);
                    if (implosion.sqrMagnitude > 1e-5f)
                    {
                        implosion.Normalize();
                    }

                    offset += (Vector3.down + implosion * 0.65f) * energy * scrEventDisplacement;
                }
                else
                {
                    offset += Vector3.up * energy * scrEventDisplacement;
                }
                if (energy > eventEnergy)
                {
                    eventEnergy = energy;
                }
            }

            eventEnergy = Mathf.Clamp01(eventEnergy);
            return offset;
        }

        private Vector3 BuildRhythmPulseOffset(float u, float v, out float rhythmEnergy)
        {
            rhythmEnergy = 0f;

            float x = u - 0.5f;
            float z = v - 0.5f;
            float distance = Mathf.Sqrt((x * x) + (z * z));
            float angle = Mathf.Atan2(z, x);
            float ibiMagnitude = Mathf.Abs(_interBeatInterval);
            float ibiDirection = _interBeatInterval < 0f ? -1f : 1f;
            float phaseSpread = ibiMagnitude * ibiPhaseSpread;
            float pulseScale = Mathf.Clamp(pulseStrength / 0.2f, 0f, 3f);
            float breathPhase = _rhythmPhase + Mathf.Sin((x - z) * Tau) * phaseSpread * ibiDirection;
            float heartMagnitude = Mathf.Abs(_heartRate);
            float breathEnergy = Mathf.Clamp01(Mathf.Sin(breathPhase * Tau) * 0.5f + 0.5f) * Mathf.Lerp(0.25f, 1f, heartMagnitude);
            float ibiRadiusBias = Mathf.Lerp(1f, ibiDirection > 0f ? 1.35f : 0.65f, ibiMagnitude);
            float ibiRingWidthBias = Mathf.Lerp(1f, ibiDirection > 0f ? 1.3f : 0.75f, ibiMagnitude);
            float ibiBreakBias = Mathf.Lerp(1f, ibiDirection > 0f ? 0.55f : 1.75f, ibiMagnitude);
            Vector3 radialDirection = distance > 0.0001f
                ? new Vector3(x / distance, 0f, z / distance)
                : Vector3.zero;
            Vector3 offset = radialDirection * breathEnergy * rhythmBreathDisplacement * pulseScale * ibiRadiusBias;

            if (_rhythmPulses == null || _rhythmPulses.Length == 0)
            {
                rhythmEnergy = breathEnergy * 0.35f;
                return offset;
            }

            for (int i = 0; i < _rhythmPulses.Length; i++)
            {
                if (!_rhythmPulses[i].Active)
                {
                    continue;
                }

                float normalizedAge = Mathf.Clamp01(_rhythmPulses[i].Age / Mathf.Max(0.0001f, _rhythmPulses[i].Lifetime));
                float stagger = Mathf.Sin(angle * Mathf.Lerp(1.5f, 5f, ibiMagnitude) + _rhythmPulses[i].PhaseSeed) * phaseSpread * ibiDirection;
                float localAge = Mathf.Clamp01(normalizedAge + stagger);
                float pulseDirection = _rhythmPulses[i].Direction < 0f ? -1f : 1f;
                float ringProgress = pulseDirection < 0f ? 1f - localAge : localAge;
                float ringRadius = _rhythmPulses[i].Radius * ringProgress * ibiRadiusBias;
                float ringWidth = Mathf.Lerp(0.025f, 0.075f, _rhythmPulses[i].Intensity) * ibiRingWidthBias;
                float ring = 1f - Mathf.Clamp01(Mathf.Abs(distance - ringRadius) / ringWidth);
                float envelope = Mathf.Sin(localAge * Mathf.PI) * (1f - localAge * 0.2f);
                float breakStrength = Mathf.Clamp01(ibiMagnitude * ibiBreakAmount * ibiBreakBias);
                float segments = Mathf.Lerp(3f, 9f, breakStrength);
                float breakPattern = Mathf.Sin(angle * segments + _rhythmPulses[i].PhaseSeed);
                float breakThreshold = Mathf.Lerp(-1f, 0.35f, breakStrength);
                float arcMask = Mathf.Lerp(1f, breakPattern > breakThreshold ? 1f : 0.12f, breakStrength);
                float energy = ring * envelope * _rhythmPulses[i].Intensity * arcMask;

                Vector3 pulseOffsetDirection = pulseDirection < 0f
                    ? Vector3.down + (-radialDirection * 0.65f)
                    : Vector3.up;
                Vector3 ibiSpacingOffset = radialDirection * ibiDirection * ibiMagnitude * energy * rhythmPulseDisplacement * pulseScale * 0.85f;
                offset += (pulseOffsetDirection * energy * rhythmPulseDisplacement * pulseScale) + ibiSpacingOffset;
                if (energy > rhythmEnergy)
                {
                    rhythmEnergy = energy;
                }
            }

            rhythmEnergy = Mathf.Clamp01(Mathf.Max(rhythmEnergy, breathEnergy * 0.35f));
            return offset;
        }

        private float ResolveTemperatureTrendWaveEnergy(float u, out Color trendColor)
        {
            trendColor = neutralColor;
            if (_temperatureTrendWaves == null || _temperatureTrendWaves.Length == 0)
            {
                return 0f;
            }

            float strongestEnergy = 0f;
            for (int i = 0; i < _temperatureTrendWaves.Length; i++)
            {
                if (!_temperatureTrendWaves[i].Active)
                {
                    continue;
                }

                float lifetime = Mathf.Max(0.0001f, _temperatureTrendWaves[i].Lifetime);
                float normalizedAge = Mathf.Clamp01(_temperatureTrendWaves[i].Age / lifetime);
                float width = Mathf.Max(0.0001f, _temperatureTrendWaves[i].Width);
                float direction = _temperatureTrendWaves[i].Direction < 0f ? -1f : 1f;
                float front = direction > 0f
                    ? Mathf.Lerp(-width, 1f + width, normalizedAge)
                    : Mathf.Lerp(1f + width, -width, normalizedAge);
                float distanceBehindFront = direction > 0f ? front - u : u - front;
                if (distanceBehindFront < 0f || distanceBehindFront > width)
                {
                    continue;
                }

                float tail = Mathf.SmoothStep(0f, 1f, 1f - distanceBehindFront / width);
                float envelope = Mathf.Sin(normalizedAge * Mathf.PI);
                float energy = tail * envelope * _temperatureTrendWaves[i].Intensity;
                if (energy <= strongestEnergy)
                {
                    continue;
                }

                strongestEnergy = energy;
                trendColor = direction > 0f ? hotTemperatureTrendColor : coldTemperatureTrendColor;
            }

            return Mathf.Clamp01(strongestEnergy);
        }

        private Vector3 BuildEngagementDriftOffset(int index, float u, float v, float loosenessResponse, float arousalMotion)
        {
            if (loosenessResponse <= 0.0001f
                || lowEngagementDriftRadius <= 0f
                || _engagementDriftDirections == null
                || _engagementDriftSeeds == null
                || index < 0
                || index >= _engagementDriftDirections.Length
                || index >= _engagementDriftSeeds.Length)
            {
                return Vector3.zero;
            }

            Vector2 seed = _engagementDriftSeeds[index];
            float noiseScale = Mathf.Max(0.1f, lowEngagementDriftNoiseScale);
            float time = _localTime * Mathf.Max(0f, lowEngagementDriftSpeed);
            float xNoise = (Mathf.PerlinNoise(seed.x + u * noiseScale + time, seed.y + v * noiseScale) - 0.5f) * 2f;
            float yNoise = (Mathf.PerlinNoise(seed.x + 13.7f + u * noiseScale, seed.y + 3.1f + time) - 0.5f) * 2f;
            float zNoise = (Mathf.PerlinNoise(seed.x + 5.9f - time, seed.y + v * noiseScale + 17.3f) - 0.5f) * 2f;
            var wandering = new Vector3(xNoise, yNoise * lowEngagementVerticalDrift, zNoise);

            float arousalDrift = Mathf.Lerp(0.75f, 1.3f, Mathf.Clamp01(arousalMotion));
            Vector3 drift = _engagementDriftDirections[index] + wandering * 0.55f;
            return drift * lowEngagementDriftRadius * loosenessResponse * arousalDrift;
        }

        private Vector3 BuildTurbulence(int index, float u, float v, float amount, float motionSpeed)
        {
            if (amount <= 0f)
            {
                return Vector3.zero;
            }

            float time = _localTime * Mathf.Max(0.01f, motionSpeed);
            float x = Mathf.PerlinNoise(u * 3.7f + time, v * 3.7f + index * 0.0031f) - 0.5f;
            float y = Mathf.PerlinNoise(u * 5.3f + 17.13f, v * 5.3f + time) - 0.5f;
            float z = Mathf.PerlinNoise(u * 4.1f - time, v * 4.1f + 29.77f) - 0.5f;
            return new Vector3(x, y, z) * amount;
        }

        private Vector3 BuildEngagementDriftDirection(int index, float u, float v)
        {
            float angle = BuildStableUnit(index, 7.13f) * Tau;
            float vertical = (BuildStableUnit(index, 11.91f) - 0.5f) * 2f * lowEngagementVerticalDrift;
            var randomDirection = new Vector3(Mathf.Cos(angle), vertical, Mathf.Sin(angle));
            var radialDirection = new Vector3(u - 0.5f, 0f, v - 0.5f);
            if (radialDirection.sqrMagnitude > 0.0001f)
            {
                radialDirection.Normalize();
            }
            else
            {
                radialDirection = randomDirection;
            }

            Vector3 direction = Vector3.Lerp(randomDirection, radialDirection, lowEngagementRadialDrift);
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
        }

        private static float BuildStableUnit(int index, float salt)
        {
            float value = Mathf.Sin((index + 1) * 12.9898f + salt * 78.233f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }

        private Color ResolveBaseColor(float valence)
        {
            return valence < 0.5f
                ? Color.Lerp(lowValenceColor, neutralColor, valence * 2f)
                : Color.Lerp(neutralColor, highValenceColor, (valence - 0.5f) * 2f);
        }

        private static float ResolveDeltaTime()
        {
            if (UnityEngine.Application.isPlaying)
            {
                return Mathf.Max(0.0001f, Time.deltaTime);
            }

            return 1f / 60f;
        }

        private bool ShouldLogNow()
        {
            if (minimumLogIntervalSeconds <= 0f)
            {
                return true;
            }

            double now = UnityEngine.Application.isPlaying
                ? Time.unscaledTimeAsDouble
                : Time.realtimeSinceStartupAsDouble;
            if (now < _nextLogTime)
            {
                return false;
            }

            _nextLogTime = now + minimumLogIntervalSeconds;
            return true;
        }
    }
}
