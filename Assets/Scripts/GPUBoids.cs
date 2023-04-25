using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.VFX;
using mj.gist.tracking;
using mj.gist;
using PrefsGUI;
using PrefsGUI.RapidGUI;

namespace BoidsSimulationOnGPU
{
    public class GPUBoids : MonoBehaviour, IGUIUser
    {
        // Boidデータの構造体
        [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
        struct BoidData
        {
            public Vector3 Velocity; // 速度
            public Vector3 Position; // 位置
        }
        // スレッドグループのスレッドのサイズ
        const int SIMULATION_BLOCK_SIZE = 256;

        #region Boids Parameters
        // 最大オブジェクト数
        [Range(256, 32768)]
        public int MaxObjectNum = 16384;


        private PrefsFloat cohesionNeighborhoodRadius;
        private PrefsFloat alignmentNeighborhoodRadius;
        private PrefsFloat separateNeighborhoodRadius;

        private PrefsFloat maxSpeed;
        private PrefsFloat maxSteerForce;

        private PrefsFloat cohesionWeight;
        private PrefsFloat alignmentWeight;
        private PrefsFloat separateWeight;

        private PrefsFloat avoidWallWeight;
        private PrefsFloat interactiveForce;

        public Vector3 WallCenter = Vector3.zero;
        public float WallDepth = 1f;

        // 壁のサイズ
        public Vector3 WallSize
        {
            get
            {
                var cam = Camera.main;
                var h = 2 * cam.orthographicSize;
                var w = 2 * cam.orthographicSize * cam.aspect;
                return new Vector3(w, h, WallDepth);
            }
        }

        #endregion

        #region Built-in Resources
        // Boidsシミュレーションを行うComputeShaderの参照
        public ComputeShader BoidsCS;
        #endregion

        #region Private Resources
        // Boidの操舵力（Force）を格納したバッファ
        GraphicsBuffer _boidForceBuffer;
        // Boidの基本データ（速度, 位置, Transformなど）を格納したバッファ
        GraphicsBuffer _boidDataBuffer;
        #endregion

        #region Accessors
        // Boidの基本データを格納したバッファを取得
        public GraphicsBuffer GetBoidDataBuffer()
        {
            return this._boidDataBuffer != null ? this._boidDataBuffer : null;
        }

        // オブジェクト数を取得
        public int GetMaxObjectNum()
        {
            return this.MaxObjectNum;
        }

        // シミュレーション領域の中心座標を返す
        public Vector3 GetSimulationAreaCenter()
        {
            return this.WallCenter;
        }

        // シミュレーション領域のボックスのサイズを返す
        public Vector3 GetSimulationAreaSize()
        {
            return this.WallSize;
        }
        #endregion

        #region GUI
        public string GetName() => "Boids";

        public void ShowGUI()
        {
            cohesionNeighborhoodRadius.DoGUI();
            alignmentNeighborhoodRadius.DoGUI();
            separateNeighborhoodRadius.DoGUI();

            maxSpeed.DoGUI();
            maxSteerForce.DoGUI();

            cohesionWeight.DoGUI();
            alignmentWeight.DoGUI();
            separateWeight.DoGUI();

            avoidWallWeight.DoGUI();

            interactiveForce.DoGUI();
        }

        public void SetupGUI()
        {
            cohesionNeighborhoodRadius = new PrefsFloat("CohesionNeighborhoodRadius", 0.5f);
            alignmentNeighborhoodRadius = new PrefsFloat("AlignmentNeighborhoodRadius", 0.5f);
            separateNeighborhoodRadius = new PrefsFloat("SeparateNeighborhoodRadius", 0.5f);

            maxSpeed = new PrefsFloat("MaxSpeed", 2f);
            maxSteerForce = new PrefsFloat("MaxSteerForce", 0.5f);

            cohesionWeight = new PrefsFloat("CohesionWeight", 1f);
            alignmentWeight = new PrefsFloat("AlignmentWeight", 1f);
            separateWeight = new PrefsFloat("SeparateWeight", 1f);

            avoidWallWeight = new PrefsFloat("AvoidWallWeight", 10f);

            interactiveForce = new PrefsFloat("interactiveForce", 5f);
        }
        #endregion

        #region MonoBehaviour Functions
        void Start()
        {
            // バッファを初期化
            InitBuffer();
        }

        void Update()
        {
            // シミュレーション
            Simulation();
        }

        void OnDestroy()
        {
            // バッファを破棄
            ReleaseBuffer();
        }

        void OnDrawGizmos()
        {
            // デバッグとしてシミュレーション領域をワイヤーフレームで描画
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(WallCenter, WallSize);
        }
        #endregion

        #region Private Functions
        // バッファを初期化
        void InitBuffer()
        {
            // バッファを初期化
            _boidDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxObjectNum, Marshal.SizeOf(typeof(BoidData)));
            _boidForceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxObjectNum, Marshal.SizeOf(typeof(Vector3)));

            // Boidデータ, Forceバッファを初期化
            var forceArr = new Vector3[MaxObjectNum];
            var boidDataArr = new BoidData[MaxObjectNum];
            for (var i = 0; i < MaxObjectNum; i++)
            {
                forceArr[i] = Vector3.zero;
                boidDataArr[i].Position = Random.insideUnitSphere * 1f;
                boidDataArr[i].Velocity = Random.insideUnitSphere * 0.1f;
            }
            _boidForceBuffer.SetData(forceArr);
            _boidDataBuffer.SetData(boidDataArr);
            forceArr = null;
            boidDataArr = null;
        }

        // シミュレーション
        void Simulation()
        {
            ComputeShader cs = BoidsCS;
            int id = -1;

            // スレッドグループの数を求める
            int threadGroupSize = Mathf.CeilToInt(MaxObjectNum / SIMULATION_BLOCK_SIZE);

            // 操舵力を計算
            id = cs.FindKernel("ForceCS"); // カーネルIDを取得
            cs.SetInt("_MaxBoidObjectNum", MaxObjectNum);
            cs.SetFloat("_CohesionNeighborhoodRadius", cohesionNeighborhoodRadius);
            cs.SetFloat("_AlignmentNeighborhoodRadius", alignmentNeighborhoodRadius);
            cs.SetFloat("_SeparateNeighborhoodRadius", separateNeighborhoodRadius);
            cs.SetFloat("_MaxSpeed", maxSpeed);
            cs.SetFloat("_MaxSteerForce", maxSteerForce);
            cs.SetFloat("_SeparateWeight", separateWeight);
            cs.SetFloat("_CohesionWeight", cohesionWeight);
            cs.SetFloat("_AlignmentWeight", alignmentWeight);
            cs.SetVector("_WallCenter", WallCenter);
            cs.SetVector("_WallSize", WallSize);
            cs.SetFloat("_AvoidWallWeight", avoidWallWeight);
            cs.SetBuffer(id, "_BoidDataBufferRead", _boidDataBuffer);
            cs.SetBuffer(id, "_BoidForceBufferWrite", _boidForceBuffer);

            cs.Dispatch(id, threadGroupSize, 1, 1); // ComputeShaderを実行


            // 操舵力から、速度と位置を計算
            id = cs.FindKernel("IntegrateCS"); // カーネルIDを取得
            cs.SetFloat("_DeltaTime", Time.deltaTime);
            cs.SetBuffer(id, "_BoidForceBufferRead", _boidForceBuffer);
            cs.SetBuffer(id, "_BoidDataBufferWrite", _boidDataBuffer);

            if (Input.GetMouseButton(0))
            {
                var mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                cs.SetVector("_InteractivePos", mouse);
                cs.SetFloat("_InteractiveForce", interactiveForce);
                cs.SetFloat("_InteractiveRange", TrackingManager.Instance.DebugSize);
            }

            cs.SetBuffer(id, "_TrackerBuffer", TrackingManager.Instance.TrackerBuffer);
            cs.SetInt("_TrackersCount", TrackingManager.Instance.TotalTrackerObjectsCount);

            cs.Dispatch(id, threadGroupSize, 1, 1); // ComputeShaderを実行
        }

        // バッファを解放
        void ReleaseBuffer()
        {
            if (_boidDataBuffer != null)
            {
                _boidDataBuffer.Release();
                _boidDataBuffer = null;
            }

            if (_boidForceBuffer != null)
            {
                _boidForceBuffer.Release();
                _boidForceBuffer = null;
            }
        }


        #endregion
    } // class
} // namespace