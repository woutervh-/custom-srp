using System;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    static void SubmitBuffer(ref ScriptableRenderContext context, CommandBuffer buffer)
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    static ComputeBuffer CreateBuffer(Matrix4x4[] data)
    {
        if (data != null && data.Length >= 1)
        {
            ComputeBuffer buffer = new ComputeBuffer(data.Length, 4 * 4 * 4);
            buffer.SetData(data);
            return buffer;
        }
        else
        {
            return null;
        }
    }

    static ComputeBuffer CreateBuffer(Vector4[] data)
    {
        if (data != null && data.Length >= 1)
        {
            ComputeBuffer buffer = new ComputeBuffer(data.Length, 4 * 4);
            buffer.SetData(data);
            return buffer;
        }
        else
        {
            return null;
        }
    }

    static ComputeBuffer CreateBuffer(Vector2Int[] data)
    {
        if (data != null && data.Length >= 1)
        {
            ComputeBuffer buffer = new ComputeBuffer(data.Length, 2 * 4);
            buffer.SetData(data);
            return buffer;
        }
        else
        {
            return null;
        }
    }
}
