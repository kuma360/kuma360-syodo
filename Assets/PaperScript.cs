using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Assertions;
using System.Runtime.InteropServices;

public class PaperScript : MonoBehaviour {

	//紙のテクスチャサイズ
	static int PAPER_W = 1334;
	static int PAPER_H = 750;

	//筆を動かした時に飛び散る飛沫の３Dオブジェクトの最大数
	static int SUMI_MAX = 200;

	//真下方向
	static Vector3 _DOWN = new Vector3 (0, -1, 0);

	//紙を初期化する色  (紙は「濃さ」を0~1で表すので、0だと白く1だと黒に近くなります。
  	//直感的に0だと黒、1だと白にしても良かったんですが無計画に作ってたらこうなりました。
	static Color resetColor = new Color (0, 0, 0, 0);



	//紙のマテリアル
	public Material _paperMat;

	//筆の3Dオブジェクト
	public GameObject _fude;

	//飛沫のプレハブ
	public GameObject _sumiPrefab;


	//紙のテクスチャ
	Texture2D _target;

	//紙に変化があったらtrue
	bool _targetUpdate = false;

	//飛沫のゲームオブジェクトバッファ
	List<GameObject> _sumiList = new List<GameObject>();

	//_sumiListにアクセスするためのインデックス
	int _sumiListIdx = 0;


	//筆が描画する時の濃さ(透明度)
	float _power = 0;
	//筆が描画する時の太さ
	float _brushSize = 0;

	//1フレーム前のマウスクリック状態
	bool _lastInput = false;

	//1フレーム前の筆の座標
	Vector3 _lastPos = Vector3.zero;
	//2フレーム前の筆の座標
	Vector3 _lastPos2 = Vector3.zero;
	//筆の加速度
	Vector3 _accel = Vector3.zero;
	//1フレーム前の加速度
	Vector3 _lastAccel = Vector3.zero;
	//筆の現在地
	Vector3 _pos = Vector3.zero;


	//当たり判定用レイヤーマスク。
	//毎フレーム同じマスクを使うのでキャッシュしておく。
	int _layermask1 = -1;
	int _layermask2 = -1;

	//レイを使いまわすので用意
	Ray _ray = new Ray (Vector3.zero,_DOWN);

	//////////////////////////////////////////////////////////////////////
	//////////////////////////////////////////////////////////////////////
	//紙を真っ白にリセット。コルーチン呼び出すだけ。
	public void reset() {
		StopCoroutine (_reset ());
		StartCoroutine (_reset ());
	}

	//実際に紙を真っ白にする処理
	//重たいので何フレームかに分けて処理を行う。
	IEnumerator _reset() {
		
		_power = 0;

		int count = 0;
		var A = new WaitForEndOfFrame ();
		for (int I = 0; I < PAPER_W; ++I) {
			for (int J = 0; J < PAPER_H; ++J) {
				_target.SetPixel (I,J,resetColor);
				++count;
			}

			//20000ピクセル塗りつぶしたら一旦処理を中断
			if (20000 < count) {
				count = 0;
				_target.Apply ();
				yield return A;
			}
		}

		_target.Apply ();
	}




	//////////////////////////////////////////////////////////////////////
	//////////////////////////////////////////////////////////////////////
	//マウスカーソルの位置の3D座標を得る
	void getInputPos(ref Vector3 pos) {
		RaycastHit _hit;
		var ray = Camera.main.ScreenPointToRay (Input.mousePosition);
		if (Physics.Raycast (ray, out _hit, 1000, _layermask1)) {
			pos = _hit.point + _hit.normal * .1f;
		}
	}






	//////////////////////////////////////////////////////////////////////
	//////////////////////////////////////////////////////////////////////
	// Use this for initialization
	void Start () {

		//筆先から飛び散る墨をたくさん用意しておく
		for (int I = 0; I < SUMI_MAX; ++I) {
			var A = Instantiate (_sumiPrefab);
			A.SetActive (false);
			_sumiList.Add (A);
		}

		//毎フレーム同じレイヤーマスクを使うのでここで初期化
		_layermask1 = LayerMask.GetMask (new string[]{ "paper", "bottom" });
		_layermask2 = LayerMask.GetMask (new string[]{"paper"});

		//紙のテクスチャを用意、濃さの情報だけあれば良いのでAlpha8で用意
		_target = new Texture2D (PAPER_W, PAPER_H,TextureFormat.Alpha8,false);
		_target.wrapMode = TextureWrapMode.Clamp;
		_paperMat.SetTexture ("_MainTex",_target);

		//紙を真っ白にしておく　
		reset ();
	}



	
	// Update is called once per frame
	void Update () {

		//trueなら「跳ね」操作中
		bool HANE = false;


		//クリックの判定
		bool input = Input.GetMouseButton (0);
		if (input) {
			//クリックしてる間、徐々に筆圧が強くなる
			_power += (1 - _power) * .3f;
		} else {
			//前のフレームに入力があって、今入力がなければ「跳ね」とみなす。
			//開発中は他にも条件を付けてたんですが、最終的にはあまり意味のないコードになってます。
			HANE = _lastInput;
		}

		{
			//筆を動かす。マウスの位置に追従する動き。
			var nowPos = _lastPos;
			getInputPos (ref nowPos);
			_pos = nowPos;
			_accel = (_pos - _lastPos);

			//筆の透明度と大きさを調整
			//早く動くほど細く、薄くなるような動き
			float speed = _accel.magnitude * .2f;
			speed = Mathf.Clamp (speed, 0, 1);
			_power *= ((1 - speed) * .8f + .2f);
			_brushSize = 0.03f * _power + 0.01f * (1 - speed) + 0.001f;
		}



		//跳ねの動作
		if (HANE) {

			RaycastHit hit;

			//ユーザーが「跳ねる」操作を行うとき、クリックを離すタイミングが遅くなりがちなので、２フレーム前の位置情報を使って「跳ね」を描画する
			_ray.origin = _lastPos2;
			if (Physics.Raycast (_ray, out hit, 1000, _layermask2)) {

				//２フレーム前の位置から１フレーム前の位置に向かう方向のベクトルを得る
				var A = (_lastPos - _lastPos2);
				var B = A.normalized;
				var C = A.magnitude;

				//動作速度を適当な数値に変換しとく、あとで利用。
				float D = Mathf.Clamp (C*50, 30, 300);
				float E = (float)(D * 3);

				//筆跡を適当な回数分割して、それぞれの位置に対して墨を描画していく
				Color H = new Color(0,0,0,0);
				for (int J = 0; J < E; ++J) {
					float P = (J / E);
					H.a = P;

					//墨の位置、透明度を計算。筆の太さの範囲内のランダムな位置に１ピクセルだけ書き込み。
					//先端方向に近づくに従って少し細くなるような計算にしてある。
					Vector2 F = UnityEngine.Random.insideUnitCircle.normalized;
					float G = (P * .5f + .5f) * 1.1f;
					var X = + (F.x * _brushSize * P * PAPER_W) + (B.x * D) * (-1 + G);
					var Z = + (F.y * _brushSize * P * PAPER_H) + (B.z * D) * (-1 + G);
					var uv = hit.textureCoord;
					var CX = (int)(uv.x * PAPER_W + X);
					var CY = (int)(uv.y * PAPER_H + Z);
					_target.SetPixel (CX, CY , H);
				}

				//紙に書き込んだのでtrue
				_targetUpdate = true;

				//飛び散る墨の３Dオブジェクトを50個発生させる
				var V = Vector3.zero;
				var dirc = _accel.normalized;
				for (int J = 0; J < 50; ++J) {
					float P = (J / 50.0f);
					Vector2 N = UnityEngine.Random.insideUnitCircle.normalized;
					V.x = dirc.x * Mathf.Abs(N.x) * P * 50;
					V.y = dirc.y * UnityEngine.Random.value * 10 - 4;
					V.z = dirc.z * Mathf.Abs(N.y) * P * 50;
					append (_lastPos, V );
				}

			}

		}



		//筆による描画
		if (input) {

			var C = new Color (0, 0, 0, 1);
			var V = Vector3.zero;

			RaycastHit hit;

			//筆跡を適当な回数分割
			float MAX = _accel.magnitude * 2 + 10;

			for (int I = 0; I < MAX; ++I) {

				//描画判定の座標
				var NOW = _lastPos + _accel * I / MAX;

				//高さは固定 紙の高さが0なのでそれより少し上方向に配置。
				NOW.y = 1;

				//紙との当たり判定
				_ray.origin = NOW;
				if (Physics.Raycast (_ray, out hit, 1000, _layermask2)) {

					//筆が遅いほど濃く太く、速いほど薄く細くなる、1箇所につき10個だけ墨を書き込む。
					for (int J = 0; J < 10; ++J) {
						var t = (UnityEngine.Random.insideUnitCircle*_brushSize);
						var uv = hit.textureCoord + t;
						_target.SetPixel ((int)(uv.x*PAPER_W), (int)(uv.y*PAPER_H), C);
					}

					//紙に書き込んだのでtrue
					_targetUpdate = true;

					{//ランダムな方向に墨の３Dオブジェクトを１つ出現させる
						V.x = _accel.x * UnityEngine.Random.value * 4 + (UnityEngine.Random.value-.5f);
						V.y = _accel.y * UnityEngine.Random.value * 2 - 2;
						V.z = _accel.z * UnityEngine.Random.value * 4 + (UnityEngine.Random.value-.5f);
						append (_lastPos, V );
					}
				}

			}


			//ブレーキがかかった時に墨の飛沫が飛ぶような表現
			//具体的には、筆の移動速度が前回のフレームより速度が下がったときに墨の３Dオブジェクトが出現。
			float p = (_lastAccel.magnitude - _accel.magnitude) ;
			p = Mathf.Clamp (p, 0, 50);
			for (int J = 0; J < p; ++J) {
				V.x = _accel.x * UnityEngine.Random.value * 15;
				V.y = _accel.y * UnityEngine.Random.value * 5 -3;
				V.z = _accel.z * UnityEngine.Random.value * 15;
				append (_lastPos, V );
			}
		}


		//筆の座標更新など
		_fude.transform.position = _pos;
		_lastPos2 = _lastPos;
		_lastPos = _pos;
		_lastAccel = _accel;
		_lastInput = input;


		//紙に墨が落ちたならテクスチャ更新
		if (_targetUpdate) {
			_targetUpdate = false;
			_target.Apply ();
		}


		//飛沫を動かす。座標が0以下の位置になったら非アクティブ化。
		for (int I = 0; I < SUMI_MAX; ++I) {
			var A = _sumiList [I];
			if (A.transform.position.y < 0) {
				A.SetActive (false);
			}
		}

	}



	//墨の飛沫を出現させる
	//非アクティブなゲームオブジェクトをアクティブ化する。
	void append(Vector3 appendPos,Vector3 accel) {

		Vector3 tmpV1 = Vector3.zero;
		Vector3 tmpV2 = Vector3.zero;
		int M = _sumiList.Count;
		int I = _sumiListIdx;
		while(0<--M) {

			var S = _sumiList [I];
			if (S.activeSelf) {
				++I;
				if (_sumiList.Count <= I) {
					I = 0;
				}
				continue;
			}
			_sumiListIdx = I;

			float scale = UnityEngine.Random.value ;
			tmpV1.x = scale;
			tmpV1.y = scale;
			tmpV1.z = scale;
			tmpV2.y = 1+scale*.5f;

			S.transform.localScale = tmpV1;
			S.transform.position = appendPos + tmpV2;

			var RB = S.gameObject.GetComponent<Rigidbody> ();
			RB.velocity = accel ;
			S.gameObject.SetActive (true);
			return;
		}

	}



	//墨の飛沫が紙に触れたら、その位置にピクセルを書き込み。
	void OnTriggerStay(Collider other) {

		var RB = other.GetComponent<Rigidbody> ();
		if (null == RB) {
			return;
		}

		RaycastHit hit;

		_ray.origin = other.transform.position + new Vector3 (0, other.transform.localScale.y * .95f, 0);
		if (Physics.Raycast (_ray, out hit,1000,_layermask2)) {

			//描画の濃さを決定。飛沫の速度が速いほど薄く。
			var C = new Color (0, 0, 0, Mathf.Clamp(1-RB.velocity.magnitude*.03f,.1f,.5f));

			//触れた位置を中心に５個のピクセルを書き込み。
			for (int J = 0; J < 5; ++J) {
				Vector2 uv = hit.textureCoord + (UnityEngine.Random.insideUnitCircle*.01f );
				_target.SetPixel ((int)(uv.x*PAPER_W), (int)(uv.y*PAPER_H), C);
			}

			//紙に書き込んだのでtrue
			_targetUpdate = true;
		}

		other.gameObject.SetActive (false);
	}



}
