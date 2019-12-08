using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.UI;

public class FlowAgentsRunner : MonoBehaviour
{
    public ComputeShader FlowAgentsShader;

    [Header("Initial values")]
    [SerializeField] private int agentCount = 8;
    [SerializeField] private int textureDimension = 1024;
    [Tooltip("Green channel gives the angle of the vectors")]
    [SerializeField] private Texture VectorField;
    [SerializeField] private Renderer[] _renderers;

    [Header("Runtime values")]
    public FlowType flowType;
    [Range(0f, 1f)] public float decay = 0.01f;

    [Header("Pixel Sort parameters")]
    [SerializeField]
    [HideInInspector]
    //The shader is set in the project window.
    private ComputeShader _pixelSorterShader = default;

    [Header("Input image")]
    [SerializeField]
    [Tooltip("Image to be pixel sorted")]
    private Texture _image = default;

    [Header("Output images to visualize")]

    [SerializeField]
    [Tooltip("Result is shown on this raw image")]
    private RawImage _rawImage = default;

    [SerializeField]
    [Tooltip("Background image to fill in the empty spaces of the sorted image")]
    private RawImage _backgroundImage = default;

    [Header("Runtime parameters")]
    [SerializeField]
    [Tooltip("Strength of the gradient")]
    private float _decayAmount = 0.01f;

    [SerializeField]
    [Tooltip("HSV -> V: Value threshold to be sorted.")]
    private float _valueThreshold = 0.9f;

    [SerializeField]
    [Tooltip("Show the background image when HSV - Value is below this amount")]
    private float _alphaCutOut = 0.01f;

    [SerializeField]
    [Tooltip("Hue similarity, how similar the hues should be to be smeared")]
    private float _hueSimilarityRange = 0.1f;

    [SerializeField]
    [Header("Optional parameters")]
    private Texture _noise = default;


    struct Agent
    {
        Vector2 pos; 	// between 0-1
        Vector2 velocity;
        float speed;
        Vector2 enforcingDirection;
        Vector3 hsv;
    };

    public enum FlowType
    {
        LeftToRight = 0,
        CenterToOut = 1,
        RandomToRandom = 2,
        CenterToHorizontal = 3,
        HorizontalEdgesToCenter = 4,
        RightToLeft = 5
    }
    private const int threadGroupCount = 8;
    private int initAgentsHandle, updateAgentsHandle, updateTrailHandle, updateImageHandle, filterImageHandle;

    private ComputeBuffer agentBuffer;

    private RenderTexture trailTexture;

    void OnValidate()
    {
        if (agentCount < threadGroupCount)
        {
            agentCount = threadGroupCount;
        }
    }
    void Start()
    {
        if (FlowAgentsShader == null)
        {
            Debug.LogError("Assign FlowAgents.compute shader");
            this.enabled = false;
            return;
        }

        if (VectorField == null)
        {
            Debug.LogError("Assign Vector Field texture");
            this.enabled = false;
            return;
        }

        initAgentsHandle = FlowAgentsShader.FindKernel("InitAgents");
        updateAgentsHandle = FlowAgentsShader.FindKernel("UpdateAgents");
        updateTrailHandle = FlowAgentsShader.FindKernel("UpdateTrail");
        updateImageHandle = FlowAgentsShader.FindKernel("UpdateImage");
        filterImageHandle = FlowAgentsShader.FindKernel("FilterImage");

        InitializeAgents();
        InitializeVectorField();
        InitializeTrail();
    }
    //initializes buffer and fills values in the compute shader
    void InitializeAgents()
    {
        Agent[] agentsData = new Agent[agentCount];
        agentBuffer = new ComputeBuffer(agentsData.Length, Marshal.SizeOf(typeof(Agent)));
        agentBuffer.SetData(agentsData);

        var imageRenderTexture = new RenderTexture(_image.width, _image.height, 0);
        imageRenderTexture.enableRandomWrite = true;
        Graphics.Blit(_image, imageRenderTexture);
        _rawImage.texture = imageRenderTexture;
        _backgroundImage.texture = _image;

        UpdateParameters();
        FlowAgentsShader.SetBuffer(initAgentsHandle, "AgentBuffer", agentBuffer);
        FlowAgentsShader.SetBuffer(updateAgentsHandle, "AgentBuffer", agentBuffer);
        FlowAgentsShader.SetBuffer(updateImageHandle, "AgentBuffer", agentBuffer);
        FlowAgentsShader.SetTexture(initAgentsHandle, "Image", imageRenderTexture);
        FlowAgentsShader.SetTexture(updateImageHandle, "Image", imageRenderTexture);
        FlowAgentsShader.SetTexture(filterImageHandle, "Image", imageRenderTexture);

        FlowAgentsShader.Dispatch(initAgentsHandle, agentCount / threadGroupCount, 1, 1);
    }

    void InitializeTrail()
    {
        trailTexture = new RenderTexture(textureDimension, textureDimension, 24);
        trailTexture.enableRandomWrite = true;
        trailTexture.Create();

        foreach (var rend in _renderers)
        {
            rend.material.mainTexture = trailTexture;
            rend.material.SetTexture("_ModTex", trailTexture);
        }


        UpdateParameters();
        FlowAgentsShader.SetVector("trailDimension", Vector2.one * textureDimension);
        FlowAgentsShader.SetTexture(updateAgentsHandle, "TrailTexture", trailTexture);
        FlowAgentsShader.SetTexture(updateTrailHandle, "TrailTexture", trailTexture);
    }

    void InitializeVectorField()
    {
        FlowAgentsShader.SetVector("vectorFieldDimension", new Vector2(VectorField.width, VectorField.height));
        FlowAgentsShader.SetTexture(updateAgentsHandle, "VectorFieldTexture", VectorField);
    }
    // Update is called once per frame
    void Update()
    {
        UpdateParameters();
        FlowAgentsShader.Dispatch(updateAgentsHandle, agentCount / threadGroupCount, 1, 1);
        FlowAgentsShader.Dispatch(updateImageHandle, agentCount / threadGroupCount, 1, 1);
        FlowAgentsShader.Dispatch(filterImageHandle, _image.width / threadGroupCount, _image.height / threadGroupCount, 1);
        // FlowAgentsShader.Dispatch(updateTrailHandle, textureDimension / threadGroupCount, textureDimension / threadGroupCount, 1);
    }

    void UpdateParameters()
    {
        FlowAgentsShader.SetInt("flowType", (int)flowType);
        FlowAgentsShader.SetFloat("decay", decay);
        FlowAgentsShader.SetFloat("decayAmount", _decayAmount);
        FlowAgentsShader.SetFloat("valueThreshold", _valueThreshold);
        FlowAgentsShader.SetFloat("alphaCutOut", _alphaCutOut);
        FlowAgentsShader.SetFloat("hueSimilarityRange", _hueSimilarityRange);
        FlowAgentsShader.SetVector("imageDimension", new Vector2(_image.width, _image.height));
    }

    void OnDestroy()
    {
        if (agentBuffer != null)
        {
            agentBuffer.Dispose();
        }
    }
}
