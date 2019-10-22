﻿#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Mathematics.math;
using Unity.Mathematics;
using UnityEngine.Rendering;
using System;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
namespace MPipeline
{
    [RequireComponent(typeof(FreeCamera))]
    [RequireComponent(typeof(Camera))]
    public unsafe sealed class MTerrainEditorTool : MonoBehaviour
    {
        public ComputeShader terrainEditShader;
        public GeometryEvent geometryEvt;
        public float paintRange = 5;
        public int value = 20;
        FreeCamera freeCamera;
        Camera cam;
        ComputeBuffer distanceBuffer;
        Action<AsyncGPUReadbackRequest> complateFunc;
        float2 uv;
        void OnEnable()
        {
            cam = GetComponent<Camera>();
            freeCamera = GetComponent<FreeCamera>();
            distanceBuffer = new ComputeBuffer(1, sizeof(float));
            complateFunc = OnFinishRead;
        }

        void OnFinishRead(AsyncGPUReadbackRequest request)
        {
            float depth = request.GetData<float>().Element(0);
            float4x4 invvp = (GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix).inverse;
            float4 worldPos = mul(invvp, float4(uv * 2 - 1, depth, 1));
            worldPos.xyz /= worldPos.w;
            MTerrain terrain = MTerrain.current;
            if (!terrain) return;
            NativeList<ulong> allMaskTree = new NativeList<ulong>(5, Allocator.Temp);
            value = Mathf.Clamp(value, 0, terrain.terrainData.allMaterials.Length - 1);
            terrain.treeRoot->GetMaterialMaskRoot(worldPos.xz, paintRange, ref allMaskTree);
            foreach (var i in allMaskTree)
            {
                var treeNodePtr = (TerrainQuadTree*)i;
                if (treeNodePtr == null) continue;
                int2 maskPos = treeNodePtr->rootPos + (int2)treeNodePtr->maskScaleOffset.yz;
                int texIndex = terrain.maskVT.GetChunkIndex(maskPos);
                if (texIndex < 0) continue;
                terrainEditShader.SetTexture(1, ShaderIDs._DestTex, terrain.maskVT.GetTexture(0));
                terrainEditShader.SetVector("_SrcDestCorner", (float4)treeNodePtr->BoundedWorldPos);
                terrainEditShader.SetInt(ShaderIDs._Count, MTerrain.MASK_RESOLUTION);
                terrainEditShader.SetInt(ShaderIDs._OffsetIndex, texIndex);
                terrainEditShader.SetVector("_Circle", float4(worldPos.xz, paintRange, (float)treeNodePtr->worldSize));
                terrainEditShader.SetFloat("_TargetValue", saturate((float)((value + 0.1) / (terrain.terrainData.allMaterials.Length - 1))));
                const int disp = MTerrain.MASK_RESOLUTION / 8;
                terrainEditShader.Dispatch(1, disp, disp, 1);
            }
            terrain.treeRoot->UpdateChunks(double3(worldPos.xz, paintRange));
        }

        void Update()
        {
            if (MTerrain.current == null)
                return;
            freeCamera.enabled = Input.GetMouseButton(1);

            if (Input.GetMouseButton(0))
            {
                CommandBuffer bf = geometryEvt.afterGeometryBuffer;
                bf.SetComputeBufferParam(terrainEditShader, 0, "_DistanceBuffer", distanceBuffer);
                bf.SetComputeTextureParam(terrainEditShader, 0, ShaderIDs._CameraDepthTexture, new RenderTargetIdentifier(ShaderIDs._CameraDepthTexture));
                uv = ((float3)Input.mousePosition).xy / float2(Screen.width, Screen.height);
                bf.SetComputeVectorParam(terrainEditShader, "_UV", float4(uv, 1, 1));
                bf.DispatchCompute(terrainEditShader, 0, 1, 1, 1);
                bf.RequestAsyncReadback(distanceBuffer, complateFunc);
            }
        }
        private void OnDisable()
        {
            distanceBuffer.Dispose();
        }
    }
}
#endif