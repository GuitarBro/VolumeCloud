using System;
using UnityEngine;
using Yangrc.VolumeCloud;

namespace UnityEngine.Rendering.PostProcessing {
	/// <summary>
	/// A <see cref="ParameterOverride{T}"/> that holds a <c>shader</c> value.
	/// </summary>
	/// <remarks>
	/// </remarks>
	[Serializable]
	public sealed class ShaderParameter : ParameterOverride<Shader> { }

	/// <summary>
	/// A <see cref="ParameterOverride{T}"/> that holds a <c>compute shader</c> value.
	/// </summary>
	/// <remarks>
	/// </remarks>
	[Serializable]
	public sealed class ComputeShaderParameter : ParameterOverride<ComputeShader> { }

	/// <summary>
	/// A <see cref="ParameterOverride{T}"/> that holds a <c>VolumeCloudConfiguration</c> value.
	/// </summary>
	/// <remarks>
	/// </remarks>
	[Serializable]
	public sealed class VCConfigParameter : ParameterOverride<VolumeCloudConfiguration> { }

	/// <summary>
	/// A <see cref="ParameterOverride{T}"/> that holds a <c>Quality</c> value.
	/// </summary>
	/// <remarks>
	/// </remarks>
	[Serializable]
	public sealed class QualityParameter : ParameterOverride<Quality> { }
}
