using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.UI;

public class PixelSort : MonoBehaviour
{
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


    void Start()
    {
        _initHandle = _pixelSorterShader.FindKernel("Init");
        _sortHandle = _pixelSorterShader.FindKernel("Sort");

        _backgroundImage.texture = _image;

        _agentCount = _image.height;
        Agent[] agentsData = new Agent[_agentCount];
        _agentBuffer = new ComputeBuffer(agentsData.Length, Marshal.SizeOf(typeof(Agent)));
        _agentBuffer.SetData(agentsData);

        var imageRenderTexture = new RenderTexture(_image.width, _image.height, 0);
        imageRenderTexture.enableRandomWrite = true;
        Graphics.Blit(_image, imageRenderTexture);
        _rawImage.texture = imageRenderTexture;

        if (!_noise)
        {
            _noise = new Texture2D(2048, 2048);
        }

        _pixelSorterShader.SetBuffer(_initHandle, "AgentBuffer", _agentBuffer);
        _pixelSorterShader.SetTexture(_initHandle, "Image", imageRenderTexture);

        _pixelSorterShader.Dispatch(_initHandle, _agentCount / _threadGroupCount, 1, 1);

        _pixelSorterShader.SetBuffer(_sortHandle, "AgentBuffer", _agentBuffer);
        _pixelSorterShader.SetTexture(_sortHandle, "Image", imageRenderTexture);
        _pixelSorterShader.SetTexture(_sortHandle, "Noise", _noise);

        _dispatchCount = 0;
        _totalDispatchCount = imageRenderTexture.width;
    }

    void Update()
    {
        if (_dispatchCount > _totalDispatchCount)
        {
            _dispatchCount = 0;
        }

        _pixelSorterShader.SetFloat("currentColumn", _dispatchCount);
        _pixelSorterShader.SetFloat("decayAmount", _decayAmount);
        _pixelSorterShader.SetFloat("valueThreshold", _valueThreshold);
        _pixelSorterShader.SetFloat("alphaCutOut", _alphaCutOut);
        _pixelSorterShader.SetFloat("hueSimilarityRange", _hueSimilarityRange);
        _pixelSorterShader.Dispatch(_sortHandle, _agentCount / _threadGroupCount, 1, 1);
        _dispatchCount++;
    }

    void OnDisable()
    {
        _agentBuffer.Dispose();
    }
    private int _dispatchCount;
    private int _totalDispatchCount;
    private int _initHandle;
    private int _sortHandle;
    private ComputeBuffer _agentBuffer;
    private int _agentCount;
    private const int _threadGroupCount = 16;
    struct Agent
    {
        Vector2 position;
        Vector3 hsv;
    };

}
