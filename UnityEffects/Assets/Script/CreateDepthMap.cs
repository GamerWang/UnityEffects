﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 生成灯光的深度图
/// </summary>
public class CreateDepthMap : MonoBehaviour 
{
    public Shader depthMapShader;
    private Camera _mainCamera;//主相机
    private Camera _lightCamera;//灯光相机
    private List<Vector4> _vList = new List<Vector4>();
	void Start () 
    {
        _lightCamera = GetComponent<Camera>();
        _lightCamera.depthTextureMode = DepthTextureMode.Depth;
        _lightCamera.clearFlags = CameraClearFlags.SolidColor;
        _lightCamera.backgroundColor = Color.white;//背景色设为白色，表示背景的地方离视点最远，不会受到阴影的影响
        _lightCamera.SetReplacementShader(depthMapShader, "RenderType");//使用替换渲染方式为知道的renderType类型生成深度图
        RenderTexture depthMap = new RenderTexture(Screen.width, Screen.height, 0);
        depthMap.format = RenderTextureFormat.ARGB32;
        _lightCamera.targetTexture = depthMap;
        //
        foreach (Camera item in Camera.allCameras)
        {
            if (item.CompareTag("MainCamera"))
            {
                _mainCamera = item;
                break;
            }
        }
	}

    void Update()
    {
        //1、	求视锥8顶点 （主相机空间中） n平面（aspect * y, tan(r/2)* n,n）  f平面（aspect*y, tan(r/2) * f, f）
        float r = (_mainCamera.fieldOfView / 180f) * Mathf.PI;
        //n平面
        Vector4 nLeftUp = new Vector4(-_mainCamera.aspect * Mathf.Tan(r / 2) * _mainCamera.nearClipPlane, Mathf.Tan(r / 2) * _mainCamera.nearClipPlane, _mainCamera.nearClipPlane, 1);
        Vector4 nRightUp = new Vector4(_mainCamera.aspect * Mathf.Tan(r / 2) * _mainCamera.nearClipPlane, Mathf.Tan(r / 2) * _mainCamera.nearClipPlane, _mainCamera.nearClipPlane, 1);
        Vector4 nLeftDonw = new Vector4(-_mainCamera.aspect * Mathf.Tan(r / 2) * _mainCamera.nearClipPlane, -Mathf.Tan(r / 2) * _mainCamera.nearClipPlane, _mainCamera.nearClipPlane, 1);
        Vector4 nRightDonw = new Vector4(_mainCamera.aspect * Mathf.Tan(r / 2) * _mainCamera.nearClipPlane, -Mathf.Tan(r / 2) * _mainCamera.nearClipPlane, _mainCamera.nearClipPlane, 1);

        //f平面
        Vector4 fLeftUp = new Vector4(-_mainCamera.aspect * Mathf.Tan(r / 2) * _mainCamera.farClipPlane, Mathf.Tan(r / 2) * _mainCamera.farClipPlane, _mainCamera.farClipPlane, 1);
        Vector4 fRightUp = new Vector4(_mainCamera.aspect * Mathf.Tan(r / 2) * _mainCamera.farClipPlane, Mathf.Tan(r / 2) * _mainCamera.farClipPlane, _mainCamera.farClipPlane, 1);
        Vector4 fLeftDonw = new Vector4(-_mainCamera.aspect * Mathf.Tan(r / 2) * _mainCamera.farClipPlane, -Mathf.Tan(r / 2) * _mainCamera.farClipPlane, _mainCamera.farClipPlane, 1);
        Vector4 fRightDonw = new Vector4(_mainCamera.aspect * Mathf.Tan(r / 2) * _mainCamera.farClipPlane, -Mathf.Tan(r / 2) * _mainCamera.farClipPlane, _mainCamera.farClipPlane, 1);

        //2、将8个顶点变换到世界空间

        Matrix4x4 mainv2w = _mainCamera.transform.localToWorldMatrix;//本来这里的矩阵使用_mainCamera.cameraToWorldMatrix,但是请看：http://docs.unity3d.com/ScriptReference/Camera-cameraToWorldMatrix.html   cameraToWorldMatrix返回的是GL风格的camera空间的矩阵，z是负的，跟untiy编辑器中的不对应，（也是坑爹的很，就不能统一吗），所以我们直接使用localToWorldMatrix
        Vector4 wnLeftUp = mainv2w * nLeftUp;
        Vector4 wnRightUp =  mainv2w * nRightUp;
        Vector4 wnLeftDonw =  mainv2w * nLeftDonw;
        Vector4 wnRightDonw =  mainv2w * nRightDonw;
        //
        Vector4 wfLeftUp =  mainv2w * fLeftUp;
        Vector4 wfRightUp =  mainv2w * fRightUp;
        Vector4 wfLeftDonw =  mainv2w * fLeftDonw;
        Vector4 wfRightDonw =  mainv2w * fRightDonw;

        //将灯光相机设置在_mainCamera视锥中心
        Vector4 nCenter = (wnLeftUp + wnRightUp + wnLeftDonw + wnRightDonw) / 4f;
        Vector4 fCenter = (wfLeftUp + wfRightUp + wfLeftDonw + wfRightDonw) / 4f;

        _lightCamera.transform.position = (nCenter + fCenter) / 2f;
        //3、	求光view矩阵
        Matrix4x4 lgihtw2v = _lightCamera.transform.worldToLocalMatrix;//本来这里使用_lightCamera.worldToCameraMatrix,但是同上面不使用_mainCamera.cameraToWorldMatrix的原因一样，我们直接使用worldToLocalMatrix
        //4、	把顶点从世界空间变换到光view空间
        Vector4 vnLeftUp = lgihtw2v * wnLeftUp;
        Vector4 vnRightUp = lgihtw2v * wnRightUp;
        Vector4 vnLeftDonw = lgihtw2v * wnLeftDonw;
        Vector4 vnRightDonw = lgihtw2v * wnLeftDonw;
        //
        Vector4 vfLeftUp = lgihtw2v * wfLeftUp;
        Vector4 vfRightUp = lgihtw2v * wfRightUp;
        Vector4 vfLeftDonw = lgihtw2v * wfLeftDonw;
        Vector4 vfRightDonw = lgihtw2v * wfRightDonw;

        _vList.Clear();
        _vList.Add(vnLeftUp);
        _vList.Add(vnRightUp);
        _vList.Add(vnLeftDonw);
        _vList.Add(vnRightDonw);

        _vList.Add(vfLeftUp);
        _vList.Add(vfRightUp);
        _vList.Add(vfLeftDonw);
        _vList.Add(vfRightDonw);
        //5、	求包围盒 (由于光锥xy轴的对称性，这里求最大包围盒就好，不是严格意义的AABB)
        float maxX = -float.MaxValue;
        float maxY = -float.MaxValue;
        float maxZ = -float.MaxValue;
        float minZ = float.MaxValue;
        for (int i = 0; i < _vList.Count; i++)
        {
            Vector4 v = _vList[i];
            if (Mathf.Abs(v.x) > maxX)
            {
                maxX = Mathf.Abs(v.x);
            }
            if (Mathf.Abs(v.y) > maxY)
            {
                maxY = Mathf.Abs(v.y);
            }
            if (v.z > maxZ)
            {
                maxZ = v.z;
            }
            else if (v.z < minZ)
            {
                minZ = v.z;
            }
        }
        //5.5 优化，如果8个顶点在光锥view空间中的z<0,那么如果n=0，就可能出现应该被渲染depthmap的物体被光锥近裁面剪裁掉的情况，所以z < 0 的情况下要延光照负方向移动光源位置以避免这种情况
        if(minZ < 0)
        {
            _lightCamera.transform.position += -_lightCamera.transform.forward.normalized * Mathf.Abs(minZ);
            maxZ = maxZ - minZ;
        }

        //6、	根据包围盒确定投影矩阵 包围盒的最大z就是f，Camera.orthographicSize由y max决定 ，还要设置Camera.aspect
        _lightCamera.orthographic = true;
        _lightCamera.aspect = maxX / maxY;
        _lightCamera.orthographicSize = maxY;
        _lightCamera.nearClipPlane = 0.0f;
        _lightCamera.farClipPlane = Mathf.Abs(maxZ);
    }
}
