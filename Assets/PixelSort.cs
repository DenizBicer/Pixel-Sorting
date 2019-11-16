using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.UI;

public class PixelSort : MonoBehaviour
{
    [SerializeField]
    private ComputeShader _pixelSorterShader = default;

    [SerializeField]
    private Texture _image = default;

    [SerializeField]
    private RawImage _rawImage = default;

    [SerializeField]
    private float _decayAmount = 0.01f;

    void Start()
    {
        _initHandle = _pixelSorterShader.FindKernel("Init");
        _sortHandle = _pixelSorterShader.FindKernel("Sort");

        _agentCount = _image.height;
        Agent[] agentsData = new Agent[_agentCount];
        _agentBuffer = new ComputeBuffer(agentsData.Length, Marshal.SizeOf(typeof(Agent)));
        _agentBuffer.SetData(agentsData);

        var imageRenderTexture = new RenderTexture(_image.width, _image.height, 0);
        imageRenderTexture.enableRandomWrite = true;
        Graphics.Blit(_image, imageRenderTexture);
        _rawImage.texture = imageRenderTexture;

        _pixelSorterShader.SetBuffer(_initHandle, "AgentBuffer", _agentBuffer);
        _pixelSorterShader.SetTexture(_initHandle, "Image", imageRenderTexture);

        _pixelSorterShader.Dispatch(_initHandle, _agentCount / _threadGroupCount, 1, 1);

        _pixelSorterShader.SetBuffer(_sortHandle, "AgentBuffer", _agentBuffer);
        _pixelSorterShader.SetTexture(_sortHandle, "Image", imageRenderTexture);

        _dispatchCount = 0;
        _totalDispatchCount = imageRenderTexture.width;
    }

    void Update()
    {
        if (_dispatchCount > _totalDispatchCount)
            return;

        _pixelSorterShader.SetFloat("currentColumn", _dispatchCount);
        _pixelSorterShader.SetFloat("decayAmount", _decayAmount);
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
    private const int _threadGroupCount = 8;
    struct Agent
    {
        Vector2 position;
        Vector3 hsv;
    };

}
