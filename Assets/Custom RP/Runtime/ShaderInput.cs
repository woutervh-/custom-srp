using UnityEngine;

public static partial class ShaderInput
{
    public static ComputeBuffer CreateComputeBuffer(Vector4[] data)
    {
        ComputeBuffer buffer = new ComputeBuffer(data.Length, 4 * 4);
        buffer.SetData(data);
        return buffer;
    }
}
