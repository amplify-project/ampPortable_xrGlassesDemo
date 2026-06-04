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

            float deltaTime = ResolveDeltaTime();
            _localTime += deltaTime * internalTimeScale;

            SmoothSignals(deltaTime);
            UpdateGeometry(deltaTime);
        }

        private void OnDisable()
        {
            if (manualDriver != null)
            {
                manualDriver.OnFrame -= OnManualDriverFrame;
            }
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
            _builtGridWidth = width;
            _builtGridHeight = height;
            _needsRebuild = false;
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

            float heartPulse = Mathf.Sin(_localTime * Mathf.Lerp(1.5f, 8f, _heartRate));
            float pulse = heartPulse * pulseStrength * Mathf.Lerp(0.03f, 0.25f, _heartRate);
            float formCompression = Mathf.Lerp(1.2f, 0.62f, _tonicEda);
            float formLift = Mathf.Lerp(0.02f, 0.2f, _tonicEda);
            float contourAmplitude = Mathf.Lerp(0.025f, 0.38f, _temperatureRate);
            float contourFrequency = Mathf.Lerp(0.35f, 1.25f, _temperatureRate);
            float contourDrift = _localTime * Mathf.Lerp(0.015f, 0.12f, _temperatureRate);
            float coherence = Mathf.Clamp01((_engagement * meshCoherence) + 0.15f);
            float noiseAmount = turbulenceStrength * (Mathf.Lerp(0.04f, 0.4f, _facialArousal) + _interBeatInterval * 0.08f) * Mathf.Lerp(1f, 0.65f, coherence);
            float morphSmoothTime = Mathf.Lerp(baseMorphSmoothTime * 1.8f, baseMorphSmoothTime * 0.3f, _facialArousal);
            Color baseColor = ResolveBaseColor(_facialValence);

            for (int i = 0; i < _vertices.Length; i++)
            {
                float u = _uValues[i];
                float v = _vValues[i];
                Vector3 slowForm = BuildSlowFormTarget(u, v, formCompression, formLift, contourAmplitude, contourFrequency, contourDrift);
                Vector3 scrTexture = BuildScrTextureOffset(u, v);
                Vector3 rhythm = BuildRhythmOffset(u, v, pulse);
                Vector3 turbulence = BuildTurbulence(i, u, v, noiseAmount);

                Vector3 target = (slowForm + scrTexture + rhythm) * visualScale;
                target += turbulence;

                _vertices[i] = Vector3.SmoothDamp(_vertices[i], target, ref _velocities[i], Mathf.Max(0.01f, morphSmoothTime), Mathf.Infinity, deltaTime);

                float localEnergy = Mathf.Clamp01(_facialArousal * 0.5f + _heartRate * 0.25f + _scrFrequency * 0.25f);
                Color vertexColor = Color.Lerp(baseColor, Color.white, localEnergy * 0.25f + Mathf.Abs(heartPulse) * 0.12f);
                vertexColor.a = Mathf.Lerp(0.35f, 1f, coherence);
                _colors[i] = vertexColor;

                _particles[i].position = _vertices[i];
                _particles[i].startColor = vertexColor;
                _particles[i].startSize = particleSize * Mathf.Lerp(0.75f, 1.8f, localEnergy);
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

        private Vector3 BuildScrTextureOffset(float u, float v)
        {
            float x = u - 0.5f;
            float z = v - 0.5f;
            float textureFrequency = Mathf.Lerp(2f, 7f, _scrFrequency);
            float textureAmplitude = Mathf.Lerp(0.004f, 0.04f, _scrFrequency);
            float texture = Mathf.Sin((x * textureFrequency + _localTime * 0.25f) * Tau) *
                Mathf.Cos((z * textureFrequency - _localTime * 0.18f) * Tau);
            return Vector3.up * texture * textureAmplitude;
        }

        private Vector3 BuildRhythmOffset(float u, float v, float pulse)
        {
            float phaseOffset = Mathf.Sin(((u - v) * 2f) * Tau) * _interBeatInterval * 0.35f;
            float localPulse = pulse * (1f + phaseOffset);
            return Vector3.up * localPulse;
        }

        private Vector3 BuildTurbulence(int index, float u, float v, float amount)
        {
            if (amount <= 0f)
            {
                return Vector3.zero;
            }

            float time = _localTime * Mathf.Lerp(0.2f, 2.5f, _facialArousal);
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
