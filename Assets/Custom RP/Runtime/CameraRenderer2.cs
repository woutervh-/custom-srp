using UnityEngine;
using UnityEngine.Rendering;

public class CameraRenderer2
{
    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, int shadowMapSize)
    {
        CommandBuffer cameraBuffer = new CommandBuffer
        {
            name = camera.name
        };

#if UNITY_EDITOR
#endif
    }
}
