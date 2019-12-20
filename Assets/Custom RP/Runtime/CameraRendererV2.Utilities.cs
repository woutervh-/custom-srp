using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRendererV2
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

    static ComputeBuffer CreateBuffer(Vector3Int[] data)
    {
        if (data != null && data.Length >= 1)
        {
            ComputeBuffer buffer = new ComputeBuffer(data.Length, 3 * 4);
            buffer.SetData(data);
            return buffer;
        }
        else
        {
            return null;
        }
    }

    static Matrix4x4 CreateWorldToShadowMatrix(ref Matrix4x4 viewMatrix, ref Matrix4x4 projectionMatrix)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            projectionMatrix.m20 = -projectionMatrix.m20;
            projectionMatrix.m21 = -projectionMatrix.m21;
            projectionMatrix.m22 = -projectionMatrix.m22;
            projectionMatrix.m23 = -projectionMatrix.m23;
        }
        Matrix4x4 scaleOffset = Matrix4x4.identity;
        scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
        scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
        return scaleOffset * (projectionMatrix * viewMatrix);
    }
}