using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Yangrc.VolumeCloud;

[Serializable]
[PostProcess(typeof(VolumeCloudPPSRenderer), PostProcessEvent.BeforeTransparent, "Custom/VolumeCloud")]
public sealed class VolumeCloudPPS : PostProcessEffectSettings {
	public ShaderParameter cloudShader = new ShaderParameter();
	public ComputeShaderParameter heightPreprocessShader = new ComputeShaderParameter();
	public ShaderParameter cloudHeightProcessShader = new ShaderParameter();
	public VCConfigParameter configuration = new VCConfigParameter();
	[Range(0, 2)]
	public IntParameter downSample = new IntParameter { value = 1 };
	public QualityParameter quality = new QualityParameter();
	public BoolParameter allowCloudFrontObject = new BoolParameter();
	public BoolParameter useHierarchicalHeightMap = new BoolParameter();
}

public sealed class VolumeCloudPPSRenderer : PostProcessEffectRenderer<VolumeCloudPPS> {
	private RenderTexture[] fullBuffer;
	private int fullBufferIndex;
	private RenderTexture undersampleBuffer;
	private Matrix4x4 prevV;
	private HaltonSequence sequence = new HaltonSequence() { radix = 3 };
	// The index of 4x4 pixels.
	private int frameIndex = 0;
	private float bayerOffsetIndex = 0;
	private bool firstFrame = true;

	private Vector2Int hiHeightLevelRange = new Vector2Int(0, 9);
	private Vector2Int heightLutTextureSize = new Vector2Int(512, 512);
	private RenderTexture heightLutTexture;
	private RenderTexture hiHeightTexture;
	private RenderTexture[] hiHeightTempTextures;

	private void OnDestroy() {
		if (this.fullBuffer != null) {
			for (int i = 0; i < fullBuffer.Length; i++) {
				fullBuffer[i].Release();
				fullBuffer[i] = null;
			}
		}
		if (this.undersampleBuffer != null) {
			this.undersampleBuffer.Release();
			this.undersampleBuffer = null;
		}
		if (hiHeightTexture != null) {
			hiHeightTexture.Release();
			hiHeightTexture = null;
		}
		if (heightLutTexture != null) {
			heightLutTexture.Release();
			heightLutTexture = null;
		}
	}

	private void GenerateHierarchicalHeightMap(ref PostProcessRenderContext context) {
		var compute = settings.heightPreprocessShader.GetValue<ComputeShader>();

		if (settings.configuration.GetValue<VolumeCloudConfiguration>().weatherTex.width != 512 || settings.configuration.GetValue<VolumeCloudConfiguration>().weatherTex.height != 512) {
			throw new UnityException("Hierarchical height map mode only supports weather tex of size 512*512!");
		}

		if (EffectBase.EnsureRenderTarget(ref heightLutTexture, heightLutTextureSize.x, heightLutTextureSize.y, RenderTextureFormat.RFloat, FilterMode.Point, randomWrite: true)) {
			var kernel = compute.FindKernel("CSMain");
			context.command.SetComputeTextureParam(compute, kernel, "heightDensityMap", settings.configuration.GetValue<VolumeCloudConfiguration>().heightDensityMap);
			context.command.SetComputeTextureParam(compute, kernel, "heightLutResult", heightLutTexture);
			context.command.DispatchCompute(compute, kernel, heightLutTextureSize.x / 32, heightLutTextureSize.y / 32, 1);
		}

		EffectBase.EnsureRenderTarget(ref hiHeightTexture, 512, 512, RenderTextureFormat.RFloat, settings.configuration.GetValue<VolumeCloudConfiguration>().weatherTex.filterMode, wrapMode: settings.configuration.GetValue<VolumeCloudConfiguration>().weatherTex.wrapMode, randomWrite: true, useMipmap: true);

		EffectBase.EnsureArray(ref hiHeightTempTextures, 10);
		for (int i = 0; i <= 9; i++) {
			EffectBase.EnsureRenderTarget(ref hiHeightTempTextures[i], 512 >> i, 512 >> i, RenderTextureFormat.RFloat, FilterMode.Point);
		}

		var hDsM = context.propertySheets.Get(settings.cloudHeightProcessShader);

		//RenderTexture previousLevel = null;//Previous level hi-height map.
		//EnsureRenderTarget(ref previousLevel, 512, 512, RenderTextureFormat.RFloat, FilterMode.Point, randomWrite: true);  //The first level is same size as weather tex.
		hDsM.properties.SetTexture("_WeatherTex", settings.configuration.GetValue<VolumeCloudConfiguration>().weatherTex);
		hDsM.properties.SetTexture("_HeightLut", heightLutTexture);
		context.command.BlitFullscreenTriangle(RuntimeUtilities.blackTexture, hiHeightTempTextures[0], hDsM, 0);   //The first pass convert weather tex into height map.
		Graphics.CopyTexture(hiHeightTempTextures[0], 0, 0, hiHeightTexture, 0, 0);   //Copy first level into target texture.

		for (int i = 1; i <= Mathf.Min(9, hiHeightLevelRange.y); i++) {
			context.command.BlitFullscreenTriangle(hiHeightTempTextures[i - 1], hiHeightTempTextures[i], hDsM, 1);
			Graphics.CopyTexture(hiHeightTempTextures[i], 0, 0, hiHeightTexture, 0, i);
		}
	}

	public override void Render(PostProcessRenderContext context) {
		if (settings.cloudShader.GetValue<Shader>() != null &&
			settings.heightPreprocessShader.GetValue<ComputeShader>() != null &&
			settings.cloudHeightProcessShader.GetValue<Shader>() != null &&
			settings.configuration.GetValue<VolumeCloudConfiguration>() != null) {
			var sheet = context.propertySheets.Get(settings.cloudShader);


			//mcam = GetComponent<Camera>();
			var width = context.screenWidth >> settings.downSample.GetValue<int>();
			var height = context.screenHeight >> settings.downSample.GetValue<int>();

			settings.configuration.GetValue< VolumeCloudConfiguration>().ApplyToSheet(sheet);

			EffectBase.EnsureArray(ref fullBuffer, 2);
			firstFrame |= EffectBase.EnsureRenderTarget(ref fullBuffer[0], width, height, RenderTextureFormat.ARGBFloat, FilterMode.Bilinear);
			firstFrame |= EffectBase.EnsureRenderTarget(ref fullBuffer[1], width, height, RenderTextureFormat.ARGBFloat, FilterMode.Bilinear);
			firstFrame |= EffectBase.EnsureRenderTarget(ref undersampleBuffer, width, height, RenderTextureFormat.ARGBFloat, FilterMode.Bilinear);

			frameIndex = (frameIndex + 1) % 16;
			fullBufferIndex = (fullBufferIndex + 1) % 2;

			if (settings.useHierarchicalHeightMap) {
				GenerateHierarchicalHeightMap(ref context);
				sheet.EnableKeyword("USE_HI_HEIGHT");
				sheet.properties.SetTexture("_HiHeightMap", hiHeightTexture);
				sheet.properties.SetInt("_HeightMapSize", hiHeightTexture.width);
				sheet.properties.SetInt("_HiHeightMinLevel", hiHeightLevelRange.x);
				sheet.properties.SetInt("_HiHeightMaxLevel", hiHeightLevelRange.y);
			} else {
				sheet.DisableKeyword("USE_HI_HEIGHT");
			}

			/* Some code is from playdead TAA. */

			//1. Pass1, Render a undersampled buffer. The buffer is dithered using bayer matrix(every 3x3 pixel) and halton sequence.
			//If it's first frame, force a high quality sample to make the initial history buffer good enough.
			if (firstFrame || settings.quality.GetValue<Quality>() == Quality.High) {
				sheet.EnableKeyword("HIGH_QUALITY");
				sheet.DisableKeyword("MEDIUM_QUALITY");
				sheet.DisableKeyword("LOW_QUALITY");
			} else if (settings.quality.GetValue<Quality>() == Quality.Normal) {
				sheet.DisableKeyword("HIGH_QUALITY");
				sheet.EnableKeyword("MEDIUM_QUALITY");
				sheet.DisableKeyword("LOW_QUALITY");
			} else if (settings.quality.GetValue<Quality>() == Quality.Low) {
				sheet.DisableKeyword("HIGH_QUALITY");
				sheet.DisableKeyword("MEDIUM_QUALITY");
				sheet.EnableKeyword("LOW_QUALITY");
			}
			if (settings.allowCloudFrontObject.GetValue<bool>()) {
				sheet.EnableKeyword("ALLOW_CLOUD_FRONT_OBJECT");
			} else {
				sheet.DisableKeyword("ALLOW_CLOUD_FRONT_OBJECT");
			}

			sheet.properties.SetVector("_ProjectionExtents", context.camera.GetProjectionExtents());
			sheet.properties.SetFloat("_RaymarchOffset", sequence.Get());
			sheet.properties.SetVector("_TexelSize", undersampleBuffer.texelSize);

			context.command.BlitFullscreenTriangle(RuntimeUtilities.blackTexture, undersampleBuffer, sheet, 0);

			//2. Pass 2, blend undersampled image with history buffer to new buffer.
			sheet.properties.SetTexture("_UndersampleCloudTex", undersampleBuffer);
			sheet.properties.SetMatrix("_PrevVP", GL.GetGPUProjectionMatrix(context.camera.projectionMatrix, false) * prevV);
			sheet.properties.SetVector("_ProjectionExtents", context.camera.GetProjectionExtents());

			if (firstFrame) {   //Wait, is this the first frame? If it is, the history buffer is empty yet. It will cause glitch if we use it directly. Fill it using the undersample buffer.
				context.command.BlitFullscreenTriangle(undersampleBuffer, fullBuffer[fullBufferIndex]);
			}
			context.command.BlitFullscreenTriangle(fullBuffer[fullBufferIndex], fullBuffer[fullBufferIndex ^ 1], sheet, 1);

			//3. Pass3, Calculate lighting, blend final cloud image with final image.
			sheet.properties.SetTexture("_CloudTex", fullBuffer[fullBufferIndex ^ 1]);
			context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 2);

			//4. Cleanup
			prevV = context.camera.worldToCameraMatrix;
			firstFrame = false;
		}
	}
}
