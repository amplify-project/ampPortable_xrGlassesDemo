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

        [Header("Particles")]
        [SerializeField] private bool renderParticles = true;
        [SerializeField, Range(0.001f, 0.12f)] private float particleSize = 0.025f;

        [Header("Colour")]
        [SerializeField] private Color lowValenceColor = new Color(0.08f, 0.35f, 1f, 0.9f);
        [SerializeField] private Color neutralColor = new Color(0.2f, 1f, 0.75f, 0.9f);
        [SerializeField] private Color highValenceColor = new Color(1f, 0.22f, 0.14f, 0.9f);

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

        private struct ScrEvent
        {
            public bool Active;
            public Vector2 CenterUv;
            public float Age;
            public float Lifetime;
            public float Radius;
            public float Intensity;
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
        }

        private ScrEvent[] _scrEvents;
        private float _scrEventAccumulator;
        private int _nextScrEventIndex;
        private float _previousScrFrequency = 0.5f;

        private ScrSpark[] _scrSparks;
        private ParticleSystem.Particle[] _scrSparkParticles;
        private int _nextScrSparkIndex;

        private RhythmPulse[] _rhythmPulses;
        private float _rhythmPhase;
        private int _nextRhythmPulseIndex;

        private int _builtGridWidth;
        private int _builtGridHeight;
        private bool _needsRebuild = true;
        private float _localTime;

        private float _tonicEda = 0.5f;
        private float _temperatureRate = 0.5f;
        private float _scrFrequency = 0.5f;
        private float _heartRate = 0.5f;
        private float _interBeatInterval = 0.5f;
        private float _facialArousal = 0.5f;
        private float _facialValence = 0.5f;
        private float _engagement = 0.5f;

        private float _targetTonicEda = 0.5f;
        private float _targetTemperatureRate = 0.5f;
        private float _targetScrFrequency = 0.5f;
        private float _targetHeartRate = 0.5f;
        private float _targetInterBeatInterval = 0.5f;
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

            float deltaTime = ResolveDeltaTime();
            _localTime += deltaTime * internalTimeScale;

            SmoothSignals(deltaTime);
            UpdateScrEvents(deltaTime);
            UpdateRhythmPulses(deltaTime);
            UpdateScrSparks(deltaTime);
            UpdateGeometry(deltaTime);
            ApplyScrSparkParticles();
        }

        private void OnDisable()
        {
            if (manualDriver != null)
            {
                manualDriver.OnFrame -= OnManualDriverFrame;
            }

            ClearScrSparks();
        }

        private void OnDestroy()
        {
            if (_mesh == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(_mesh);
            }
            else
            {
                DestroyImmediate(_mesh);
            }
        }

        private void OnValidate()
        {
            gridWidth = Mathf.Max(4, gridWidth);
            gridHeight = Mathf.Max(4, gridHeight);
            _needsRebuild = true;

            if (isActiveAndEnabled)
            {
                ResolveComponents();
                EnsureInitialized();
                EnsureScrEventCapacity();
                EnsureScrSparkCapacity();
                EnsureScrSparkParticleSystem();
                EnsureRhythmPulseCapacity();
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

            if (scrSparkParticleSystem != null)
            {
                return;
            }

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
                    return;
                }
            }

            scrSparkParticleSystem = fallbackParticleSystem;
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

            float sustainedDrive = Mathf.InverseLerp(scrEventThreshold, 1f, _scrFrequency);
            float riseDrive = Mathf.Max(0f, _scrFrequency - _previousScrFrequency) * Mathf.Max(0f, scrRiseEventGain);
            _scrEventAccumulator += sustainedDrive * Mathf.Max(0f, scrEventsPerSecondAtMax) * safeDeltaTime;
            _scrEventAccumulator += riseDrive;

            int spawnedThisFrame = 0;
            while (_scrEventAccumulator >= 1f && spawnedThisFrame < 4)
            {
                SpawnScrEvent(Mathf.Clamp01(Mathf.Max(_scrFrequency, sustainedDrive)));
                _scrEventAccumulator -= 1f;
                spawnedThisFrame++;
            }

            if (_scrEventAccumulator > 4f)
            {
                _scrEventAccumulator = 4f;
            }

            _previousScrFrequency = _scrFrequency;
        }

        private void SpawnScrEvent(float intensity)
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
            float clampedIntensity = Mathf.Clamp01(intensity);

            _scrEvents[eventIndex] = new ScrEvent
            {
                Active = true,
                CenterUv = new Vector2(u, v),
                Age = 0f,
                Lifetime = Mathf.Max(0.1f, scrEventLifetime) * Mathf.Lerp(0.75f, 1.35f, clampedIntensity),
                Radius = Mathf.Max(0.01f, scrEventRadius) * Mathf.Lerp(0.75f, 1.4f, clampedIntensity),
                Intensity = Mathf.Lerp(0.35f, 1f, clampedIntensity)
            };

            SpawnScrSparks(new Vector2(u, v), clampedIntensity);
        }

        private void SpawnScrSparks(Vector2 centerUv, float intensity)
        {
            EnsureScrSparkCapacity();
            if (_scrSparks == null || _scrSparks.Length == 0 || scrSparksPerEventAtMax <= 0)
            {
                return;
            }

            float clampedIntensity = Mathf.Clamp01(intensity);
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
                _scrSparks[sparkIndex] = new ScrSpark
                {
                    Active = true,
                    Position = origin + lateral * 0.2f,
                    Velocity = (lateral / lifetime) + (Vector3.up * lift),
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
            float rateMultiplier = Mathf.Lerp(0.85f, 1f + Mathf.Max(0f, rhythmRateStdDevBoost), _heartRate);
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

        private void SpawnRhythmPulse(float intensity)
        {
            if (_rhythmPulses == null || _rhythmPulses.Length == 0)
            {
                return;
            }

            int pulseIndex = _nextRhythmPulseIndex % _rhythmPulses.Length;
            float placementIndex = _nextRhythmPulseIndex;
            _nextRhythmPulseIndex++;

            float clampedIntensity = Mathf.Clamp01(intensity);
            _rhythmPulses[pulseIndex] = new RhythmPulse
            {
                Active = true,
                Age = 0f,
                Lifetime = Mathf.Max(0.1f, rhythmPulseLifetime) * Mathf.Lerp(1.15f, 0.85f, clampedIntensity),
                Radius = Mathf.Max(0.01f, rhythmPulseRadius) * Mathf.Lerp(0.85f, 1.25f, clampedIntensity),
                Intensity = Mathf.Lerp(0.35f, 1f, clampedIntensity),
                PhaseSeed = placementIndex * 1.6180339f
            };
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
            if (_mesh == null || _vertices == null || _particles == null)
            {
                return;
            }

            float formCompression = Mathf.Lerp(1.2f, 0.62f, _tonicEda);
            float formLift = Mathf.Lerp(0.02f, 0.2f, _tonicEda);
            float contourAmplitude = Mathf.Lerp(0.025f, 0.38f, _temperatureRate);
            float contourFrequency = Mathf.Lerp(0.35f, 1.25f, _temperatureRate);
            float contourDrift = _localTime * Mathf.Lerp(0.015f, 0.12f, _temperatureRate);
            float arousalMotion = Mathf.Clamp01(_facialArousal);
            float engagementSolidity = Mathf.Clamp01(_engagement);
            float coherence = Mathf.Clamp01((engagementSolidity * meshCoherence * engagementMeshSurfaceStrength) + 0.15f);
            float arousalTurbulence = Mathf.Lerp(arousalTurbulenceMin, arousalTurbulenceMax, arousalMotion);
            float affectMotionSpeed = Mathf.Lerp(arousalMotionSpeedMin, arousalMotionSpeedMax, arousalMotion);
            float noiseAmount = turbulenceStrength * (arousalTurbulence + _interBeatInterval * 0.08f) * Mathf.Lerp(1f, 0.65f, coherence);
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

                Vector3 target = (slowForm + scrEventOffset + rhythm) * visualScale;
                target += turbulence;

                _vertices[i] = Vector3.SmoothDamp(_vertices[i], target, ref _velocities[i], Mathf.Max(0.01f, morphSmoothTime), Mathf.Infinity, deltaTime);

                float localEnergy = Mathf.Clamp01(arousalMotion * 0.5f + _heartRate * 0.25f + _scrFrequency * 0.25f);
                Color vertexColor = Color.Lerp(valenceColor, Color.white, Mathf.Clamp01(localEnergy * 0.25f + rhythmEnergy * 0.35f + scrEventEnergy * 0.65f));
                vertexColor.a = Mathf.Lerp(engagementAlpha, highEngagementAlpha, Mathf.Max(rhythmEnergy * 0.25f, scrEventEnergy * 0.35f));
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
                float ringRadius = _scrEvents[i].Radius * normalizedAge;
                float ringWidth = Mathf.Lerp(0.025f, 0.07f, _scrEvents[i].Intensity);
                float distance = Vector2.Distance(uv, _scrEvents[i].CenterUv);
                float ring = 1f - Mathf.Clamp01(Mathf.Abs(distance - ringRadius) / ringWidth);
                float envelope = Mathf.Sin(normalizedAge * Mathf.PI) * (1f - normalizedAge * 0.35f);
                float energy = ring * envelope * _scrEvents[i].Intensity;

                offset += Vector3.up * energy * scrEventDisplacement;
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
            float phaseSpread = _interBeatInterval * ibiPhaseSpread;
            float pulseScale = Mathf.Clamp(pulseStrength / 0.2f, 0f, 3f);
            float breathPhase = _rhythmPhase + Mathf.Sin((x - z) * Tau) * phaseSpread;
            float breathEnergy = Mathf.Clamp01(Mathf.Sin(breathPhase * Tau) * 0.5f + 0.5f) * Mathf.Lerp(0.25f, 1f, _heartRate);
            Vector3 radialDirection = distance > 0.0001f
                ? new Vector3(x / distance, 0f, z / distance)
                : Vector3.zero;
            Vector3 offset = radialDirection * breathEnergy * rhythmBreathDisplacement * pulseScale;

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
                float stagger = Mathf.Sin(angle * Mathf.Lerp(1.5f, 5f, _interBeatInterval) + _rhythmPulses[i].PhaseSeed) * phaseSpread;
                float localAge = Mathf.Clamp01(normalizedAge + stagger);
                float ringRadius = _rhythmPulses[i].Radius * localAge;
                float ringWidth = Mathf.Lerp(0.025f, 0.075f, _rhythmPulses[i].Intensity);
                float ring = 1f - Mathf.Clamp01(Mathf.Abs(distance - ringRadius) / ringWidth);
                float envelope = Mathf.Sin(localAge * Mathf.PI) * (1f - localAge * 0.2f);
                float breakStrength = Mathf.Clamp01(_interBeatInterval * ibiBreakAmount);
                float segments = Mathf.Lerp(3f, 9f, breakStrength);
                float breakPattern = Mathf.Sin(angle * segments + _rhythmPulses[i].PhaseSeed);
                float breakThreshold = Mathf.Lerp(-1f, 0.35f, breakStrength);
                float arcMask = Mathf.Lerp(1f, breakPattern > breakThreshold ? 1f : 0.12f, breakStrength);
                float energy = ring * envelope * _rhythmPulses[i].Intensity * arcMask;

                offset += Vector3.up * energy * rhythmPulseDisplacement * pulseScale;
                if (energy > rhythmEnergy)
                {
                    rhythmEnergy = energy;
                }
            }

            rhythmEnergy = Mathf.Clamp01(Mathf.Max(rhythmEnergy, breathEnergy * 0.35f));
            return offset;
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
