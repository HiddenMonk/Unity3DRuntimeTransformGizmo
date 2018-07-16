using System;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace RuntimeGizmos
{
	[RequireComponent(typeof(Camera))]
	public class TransformGizmo : MonoBehaviour
	{
		public TransformSpace space = TransformSpace.Global;
		public TransformType type = TransformType.Move;
		public TransformPivot pivot = TransformPivot.Pivot;

		//These are the same as the unity editor hotkeys
		public KeyCode SetPivotModeToggle = KeyCode.Z;
		public KeyCode SetMoveType = KeyCode.W;
		public KeyCode SetRotateType = KeyCode.E;
		public KeyCode SetScaleType = KeyCode.R;
		public KeyCode SetSpaceToggle = KeyCode.X;

		Color xColor = new Color(1, 0, 0, 0.8f);
		Color yColor = new Color(0, 1, 0, 0.8f);
		Color zColor = new Color(0, 0, 1, 0.8f);
		Color allColor = new Color(.7f, .7f, .7f, 0.8f);
		Color selectedColor = new Color(1, 1, 0, 0.8f);

		float handleLength = .25f;
		float triangleSize = .03f;
		float boxSize = .01f;
		int circleDetail = 40;
		float minSelectedDistanceCheck = .04f;
		float moveSpeedMultiplier = 1f;
		float scaleSpeedMultiplier = 1f;
		float rotateSpeedMultiplier = 200f;
		float allRotateSpeedMultiplier = 20f;

		AxisVectors handleLines = new AxisVectors();
		AxisVectors handleTriangles = new AxisVectors();
		AxisVectors handleSquares = new AxisVectors();
		AxisVectors circlesLines = new AxisVectors();
		AxisVectors drawCurrentCirclesLines = new AxisVectors();
		
		bool isTransforming;
		float totalScaleAmount;
		Quaternion totalRotationAmount;
		Axis selectedAxis = Axis.None;
		AxisInfo axisInfo;
		Transform target;
		Camera myCamera;

		static Material lineMaterial;
		static Transform pivotTransform;

		void Awake()
		{
			myCamera = GetComponent<Camera>();
			SetMaterial();
		}

		void Update()
		{
			SetSpaceAndType();
			SelectAxis();
			GetTarget();
			if(target == null) return;
			
			TransformSelected();
		}

		void LateUpdate()
		{
			if(target == null) return;

			//We run this in lateupdate since coroutines run after update and we want our gizmos to have the updated target transform position after TransformSelected()
			SetAxisInfo();
			SetLines();
		}

		void OnPostRender()
		{
			if(target == null) return;

			lineMaterial.SetPass(0);

			Color xColor = (selectedAxis == Axis.X) ? selectedColor : this.xColor;
			Color yColor = (selectedAxis == Axis.Y) ? selectedColor : this.yColor;
			Color zColor = (selectedAxis == Axis.Z) ? selectedColor : this.zColor;
			Color allColor = (selectedAxis == Axis.Any) ? selectedColor : this.allColor;

			DrawLines(handleLines.x, xColor);
			DrawLines(handleLines.y, yColor);
			DrawLines(handleLines.z, zColor);

			DrawTriangles(handleTriangles.x, xColor);
			DrawTriangles(handleTriangles.y, yColor);
			DrawTriangles(handleTriangles.z, zColor);

			DrawSquares(handleSquares.x, xColor);
			DrawSquares(handleSquares.y, yColor);
			DrawSquares(handleSquares.z, zColor);
			DrawSquares(handleSquares.all, allColor);

			AxisVectors rotationAxisVector = circlesLines;
			if(isTransforming && space == TransformSpace.Global && type == TransformType.Rotate)
			{
				rotationAxisVector = drawCurrentCirclesLines;

				AxisInfo axisInfo = new AxisInfo();
				axisInfo.xDirection = totalRotationAmount * Vector3.right;
				axisInfo.yDirection = totalRotationAmount * Vector3.up;
				axisInfo.zDirection = totalRotationAmount * Vector3.forward;
				SetCircles(axisInfo, drawCurrentCirclesLines);
			}

			DrawCircles(rotationAxisVector.x, xColor);
			DrawCircles(rotationAxisVector.y, yColor);
			DrawCircles(rotationAxisVector.z, zColor);
			DrawCircles(rotationAxisVector.all, allColor);
		}

		void SetSpaceAndType()
		{
			if(Input.GetKeyDown(SetMoveType)) type = TransformType.Move;
			else if(Input.GetKeyDown(SetRotateType)) type = TransformType.Rotate;
			else if(Input.GetKeyDown(SetScaleType)) type = TransformType.Scale;

			if(Input.GetKeyDown(SetPivotModeToggle))
			{
				if(pivot == TransformPivot.Pivot) pivot = TransformPivot.Center;
				else if(pivot == TransformPivot.Center) pivot = TransformPivot.Pivot;
				SetTargetPivot();
			}

			if(Input.GetKeyDown(SetSpaceToggle))
			{
				if(space == TransformSpace.Global) space = TransformSpace.Local;
				else if(space == TransformSpace.Local) space = TransformSpace.Global;
			}

			if(type == TransformType.Scale) space = TransformSpace.Local; //Only support local scale
		}

		void TransformSelected()
		{
			if(selectedAxis != Axis.None && Input.GetMouseButtonDown(0))
			{
				StartCoroutine(TransformSelected(type));
			}
		}
		
		IEnumerator TransformSelected(TransformType type)
		{
			isTransforming = true;
			totalScaleAmount = 0;
			totalRotationAmount = Quaternion.identity;

			Vector3 originalTargetPosition = target.position;
			Vector3 planeNormal = (transform.position - target.position).normalized;
			Vector3 axis = GetSelectedAxisDirection();
			Vector3 projectedAxis = Vector3.ProjectOnPlane(axis, planeNormal).normalized;
			Vector3 previousMousePosition = Vector3.zero;

			while(!Input.GetMouseButtonUp(0))
			{
				Ray mouseRay = myCamera.ScreenPointToRay(Input.mousePosition);
				Vector3 mousePosition = Geometry.LinePlaneIntersect(mouseRay.origin, mouseRay.direction, originalTargetPosition, planeNormal);

				if(previousMousePosition != Vector3.zero && mousePosition != Vector3.zero)
				{
					if(type == TransformType.Move)
					{
						float moveAmount = ExtVector3.MagnitudeInDirection(mousePosition - previousMousePosition, projectedAxis) * moveSpeedMultiplier;
						target.Translate(axis * moveAmount, Space.World);
					}

					if(type == TransformType.Scale)
					{
						Vector3 projected = (selectedAxis == Axis.Any) ? transform.right : projectedAxis;
						float scaleAmount = ExtVector3.MagnitudeInDirection(mousePosition - previousMousePosition, projected) * scaleSpeedMultiplier;
						
						//WARNING - There is a bug in unity 5.4 and 5.5 that causes InverseTransformDirection to be affected by scale which will break negative scaling. Not tested, but updating to 5.4.2 should fix it - https://issuetracker.unity3d.com/issues/transformdirection-and-inversetransformdirection-operations-are-affected-by-scale
						Vector3 localAxis = (space == TransformSpace.Local && selectedAxis != Axis.Any) ? target.InverseTransformDirection(axis) : axis;
						
						if(selectedAxis == Axis.Any) target.localScale += (ExtVector3.Abs(target.localScale.normalized) * scaleAmount);
						else target.localScale += (localAxis * scaleAmount);
					
						totalScaleAmount += scaleAmount;
					}

					if(type == TransformType.Rotate)
					{
						if(selectedAxis == Axis.Any)
						{
							Vector3 rotation = transform.TransformDirection(new Vector3(Input.GetAxis("Mouse Y"), -Input.GetAxis("Mouse X"), 0));
							target.Rotate(rotation * allRotateSpeedMultiplier, Space.World);
							totalRotationAmount *= Quaternion.Euler(rotation * allRotateSpeedMultiplier);
						}else{
							Vector3 projected = (selectedAxis == Axis.Any || ExtVector3.IsParallel(axis, planeNormal)) ? planeNormal : Vector3.Cross(axis, planeNormal);
							float rotateAmount = (ExtVector3.MagnitudeInDirection(mousePosition - previousMousePosition, projected) * rotateSpeedMultiplier) / GetDistanceMultiplier();
							target.Rotate(axis, rotateAmount, Space.World);
							totalRotationAmount *= Quaternion.Euler(axis * rotateAmount);
						}
					}
				}

				previousMousePosition = mousePosition;

				yield return null;
			}

			totalRotationAmount = Quaternion.identity;
			totalScaleAmount = 0;
			isTransforming = false;
		}

		Vector3 GetSelectedAxisDirection()
		{
			if(selectedAxis != Axis.None)
			{
				if(selectedAxis == Axis.X) return axisInfo.xDirection;
				if(selectedAxis == Axis.Y) return axisInfo.yDirection;
				if(selectedAxis == Axis.Z) return axisInfo.zDirection;
				if(selectedAxis == Axis.Any) return Vector3.one;
			}
			return Vector3.zero;
		}
	
		void GetTarget()
		{
			if(selectedAxis == Axis.None && Input.GetMouseButtonDown(0))
			{
				DestroyTargetPivot();
			
				RaycastHit hitInfo; 
				if(Physics.Raycast(myCamera.ScreenPointToRay(Input.mousePosition), out hitInfo))
				{
					target = hitInfo.transform;
					SetTargetPivot();
				}else{
					target = null;
				}
			}
		}
		
		void SetTargetPivot()
		{
			DestroyTargetPivot();

			if(target == null) return;

			if(pivot == TransformPivot.Pivot)
			{
				//The target position is the pivot, so no need to do anything.
				return;
			}
			else if(pivot == TransformPivot.Center)
			{
				Renderer targetRenderer = target.GetComponent<Renderer>();
				if(targetRenderer == null)
				{
	#if UNITY_EDITOR
					Debug.LogWarning("Runtime TransformGizmo cannot use Pivot Center mode since there is no Renderer on the selected object.");
	#endif
					return;
				}

				if(pivotTransform == null)
				{
					pivotTransform = new GameObject("TemporaryTransformGizmoPivot").transform;
				}

				pivotTransform.position = targetRenderer.bounds.center;
				pivotTransform.rotation = target.rotation;

				if(target.parent != null)
				{
					pivotTransform.SetParent(target.parent, true);
				}

				target.SetParent(pivotTransform, true);
				target = pivotTransform;
			}
		}

		void DestroyTargetPivot()
		{
			if(pivotTransform != null)
			{
				if(pivotTransform.childCount > 0)
				{
					target = pivotTransform.GetChild(0);
					target.SetParent(pivotTransform.parent, true);
				}else{
					target = null;
				}

				GameObject.Destroy(pivotTransform.gameObject);
				pivotTransform = null; //Need to do this so we can see right away that its destroyed, else we wont know until next frame.
			}
		}

		AxisVectors selectedLinesBuffer = new AxisVectors();
		void SelectAxis()
		{
			if(!Input.GetMouseButtonDown(0)) return;
			selectedAxis = Axis.None;

			float xClosestDistance = float.MaxValue;
			float yClosestDistance = float.MaxValue;
			float zClosestDistance = float.MaxValue;
			float allClosestDistance = float.MaxValue;
			float minSelectedDistanceCheck = this.minSelectedDistanceCheck * GetDistanceMultiplier();

			if(type == TransformType.Move || type == TransformType.Scale)
			{
				selectedLinesBuffer.Clear();
				selectedLinesBuffer.Add(handleLines);
				if(type == TransformType.Move) selectedLinesBuffer.Add(handleTriangles);
				else if(type == TransformType.Scale) selectedLinesBuffer.Add(handleSquares);

				xClosestDistance = ClosestDistanceFromMouseToLines(selectedLinesBuffer.x);
				yClosestDistance = ClosestDistanceFromMouseToLines(selectedLinesBuffer.y);
				zClosestDistance = ClosestDistanceFromMouseToLines(selectedLinesBuffer.z);
				allClosestDistance = ClosestDistanceFromMouseToLines(selectedLinesBuffer.all);
			}
			else if(type == TransformType.Rotate)
			{
				xClosestDistance = ClosestDistanceFromMouseToLines(circlesLines.x);
				yClosestDistance = ClosestDistanceFromMouseToLines(circlesLines.y);
				zClosestDistance = ClosestDistanceFromMouseToLines(circlesLines.z);
				allClosestDistance = ClosestDistanceFromMouseToLines(circlesLines.all);
			}

			if(type == TransformType.Scale && allClosestDistance <= minSelectedDistanceCheck) selectedAxis = Axis.Any;
			else if(xClosestDistance <= minSelectedDistanceCheck && xClosestDistance <= yClosestDistance && xClosestDistance <= zClosestDistance) selectedAxis = Axis.X;
			else if(yClosestDistance <= minSelectedDistanceCheck && yClosestDistance <= xClosestDistance && yClosestDistance <= zClosestDistance) selectedAxis = Axis.Y;
			else if(zClosestDistance <= minSelectedDistanceCheck && zClosestDistance <= xClosestDistance && zClosestDistance <= yClosestDistance) selectedAxis = Axis.Z;
			else if(type == TransformType.Rotate && target != null)
			{
				Ray mouseRay = myCamera.ScreenPointToRay(Input.mousePosition);
				Vector3 mousePlaneHit = Geometry.LinePlaneIntersect(mouseRay.origin, mouseRay.direction, target.position, (transform.position - target.position).normalized);
				if((target.position - mousePlaneHit).sqrMagnitude <= (handleLength * GetDistanceMultiplier()).Squared()) selectedAxis = Axis.Any;
			}
		}

		float ClosestDistanceFromMouseToLines(List<Vector3> lines)
		{
			Ray mouseRay = myCamera.ScreenPointToRay(Input.mousePosition);

			float closestDistance = float.MaxValue;
			for(int i = 0; i < lines.Count; i += 2)
			{
				IntersectPoints points = Geometry.ClosestPointsOnSegmentToLine(lines[i], lines[i + 1], mouseRay.origin, mouseRay.direction);
				float distance = Vector3.Distance(points.first, points.second);
				if(distance < closestDistance)
				{
					closestDistance = distance;
				}
			}
			return closestDistance;
		}

		void SetAxisInfo()
		{
			float size = handleLength * GetDistanceMultiplier();
			axisInfo.Set(target, size, space);

			if(isTransforming && type == TransformType.Scale)
			{
				if(selectedAxis == Axis.Any) axisInfo.Set(target, size + totalScaleAmount, space);
				if(selectedAxis == Axis.X) axisInfo.xAxisEnd += (axisInfo.xDirection * totalScaleAmount);
				if(selectedAxis == Axis.Y) axisInfo.yAxisEnd += (axisInfo.yDirection * totalScaleAmount);
				if(selectedAxis == Axis.Z) axisInfo.zAxisEnd += (axisInfo.zDirection * totalScaleAmount);
			}
		}

		//This helps keep the size consistent no matter how far we are from it.
		float GetDistanceMultiplier()
		{
			if(target == null) return 0f;
			return Mathf.Max(.01f, Mathf.Abs(ExtVector3.MagnitudeInDirection(target.position - transform.position, myCamera.transform.forward)));
		}

		void SetLines()
		{
			SetHandleLines();
			SetHandleTriangles();
			SetHandleSquares();
			SetCircles(axisInfo, circlesLines);
		}

		void SetHandleLines()
		{
			handleLines.Clear();

			if(type == TransformType.Move || type == TransformType.Scale)
			{
				handleLines.x.Add(target.position);
				handleLines.x.Add(axisInfo.xAxisEnd);
				handleLines.y.Add(target.position);
				handleLines.y.Add(axisInfo.yAxisEnd);
				handleLines.z.Add(target.position);
				handleLines.z.Add(axisInfo.zAxisEnd);
			}
		}

		void SetHandleTriangles()
		{
			handleTriangles.Clear();

			if(type == TransformType.Move)
			{
				float triangleLength = triangleSize * GetDistanceMultiplier();
				AddTriangles(axisInfo.xAxisEnd, axisInfo.xDirection, axisInfo.yDirection, axisInfo.zDirection, triangleLength, handleTriangles.x);
				AddTriangles(axisInfo.yAxisEnd, axisInfo.yDirection, axisInfo.xDirection, axisInfo.zDirection, triangleLength, handleTriangles.y);
				AddTriangles(axisInfo.zAxisEnd, axisInfo.zDirection, axisInfo.yDirection, axisInfo.xDirection, triangleLength, handleTriangles.z);
			}
		}

		void AddTriangles(Vector3 axisEnd, Vector3 axisDirection, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float size, List<Vector3> resultsBuffer)
		{
			Vector3 endPoint = axisEnd + (axisDirection * (size * 2f));
			Square baseSquare = GetBaseSquare(axisEnd, axisOtherDirection1, axisOtherDirection2, size / 2f);

			resultsBuffer.Add(baseSquare.bottomLeft);
			resultsBuffer.Add(baseSquare.topLeft);
			resultsBuffer.Add(baseSquare.topRight);
			resultsBuffer.Add(baseSquare.topLeft);
			resultsBuffer.Add(baseSquare.bottomRight);
			resultsBuffer.Add(baseSquare.topRight);

			for(int i = 0; i < 4; i++)
			{
				resultsBuffer.Add(baseSquare[i]);
				resultsBuffer.Add(baseSquare[i + 1]);
				resultsBuffer.Add(endPoint);
			}
		}

		void SetHandleSquares()
		{
			handleSquares.Clear();

			if(type == TransformType.Scale)
			{
				float boxLength = boxSize * GetDistanceMultiplier();
				AddSquares(axisInfo.xAxisEnd, axisInfo.xDirection, axisInfo.yDirection, axisInfo.zDirection, boxLength, handleSquares.x);
				AddSquares(axisInfo.yAxisEnd, axisInfo.yDirection, axisInfo.xDirection, axisInfo.zDirection, boxLength, handleSquares.y);
				AddSquares(axisInfo.zAxisEnd, axisInfo.zDirection, axisInfo.xDirection, axisInfo.yDirection, boxLength, handleSquares.z);
				AddSquares(target.position - (axisInfo.xDirection * boxLength), axisInfo.xDirection, axisInfo.yDirection, axisInfo.zDirection, boxLength, handleSquares.all);
			}
		}

		void AddSquares(Vector3 axisEnd, Vector3 axisDirection, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float size, List<Vector3> resultsBuffer)
		{
			Square baseSquare = GetBaseSquare(axisEnd, axisOtherDirection1, axisOtherDirection2, size);
			Square baseSquareEnd = GetBaseSquare(axisEnd + (axisDirection * (size * 2f)), axisOtherDirection1, axisOtherDirection2, size);

			resultsBuffer.Add(baseSquare.bottomLeft);
			resultsBuffer.Add(baseSquare.topLeft);
			resultsBuffer.Add(baseSquare.bottomRight);
			resultsBuffer.Add(baseSquare.topRight);

			resultsBuffer.Add(baseSquareEnd.bottomLeft);
			resultsBuffer.Add(baseSquareEnd.topLeft);
			resultsBuffer.Add(baseSquareEnd.bottomRight);
			resultsBuffer.Add(baseSquareEnd.topRight);

			for(int i = 0; i < 4; i++)
			{
				resultsBuffer.Add(baseSquare[i]);
				resultsBuffer.Add(baseSquare[i + 1]);
				resultsBuffer.Add(baseSquareEnd[i + 1]);
				resultsBuffer.Add(baseSquareEnd[i]);
			}
		}

		Square GetBaseSquare(Vector3 axisEnd, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float size)
		{
			Square square;
			Vector3 offsetUp = ((axisOtherDirection1 * size) + (axisOtherDirection2 * size));
			Vector3 offsetDown = ((axisOtherDirection1 * size) - (axisOtherDirection2 * size));
			//These arent really the proper directions, as in the bottomLeft isnt really at the bottom left...
			square.bottomLeft = axisEnd + offsetDown;
			square.topLeft = axisEnd + offsetUp;
			square.bottomRight = axisEnd - offsetDown;
			square.topRight = axisEnd - offsetUp;
			return square;
		}

		void SetCircles(AxisInfo axisInfo, AxisVectors axisVectors)
		{
			axisVectors.Clear();

			if(type == TransformType.Rotate)
			{
				float circleLength = handleLength * GetDistanceMultiplier();
				AddCircle(target.position, axisInfo.xDirection, circleLength, axisVectors.x);
				AddCircle(target.position, axisInfo.yDirection, circleLength, axisVectors.y);
				AddCircle(target.position, axisInfo.zDirection, circleLength, axisVectors.z);
				AddCircle(target.position, (target.position - transform.position).normalized, circleLength, axisVectors.all, false);
			}
		}

		void AddCircle(Vector3 origin, Vector3 axisDirection, float size, List<Vector3> resultsBuffer, bool depthTest = true)
		{
			Vector3 up = axisDirection.normalized * size;
			Vector3 forward = Vector3.Slerp(up, -up, .5f);
			Vector3 right = Vector3.Cross(up, forward).normalized * size;
		
			Matrix4x4 matrix = new Matrix4x4();
		
			matrix[0] = right.x;
			matrix[1] = right.y;
			matrix[2] = right.z;
		
			matrix[4] = up.x;
			matrix[5] = up.y;
			matrix[6] = up.z;
		
			matrix[8] = forward.x;
			matrix[9] = forward.y;
			matrix[10] = forward.z;
		
			Vector3 lastPoint = origin + matrix.MultiplyPoint3x4(new Vector3(Mathf.Cos(0), 0, Mathf.Sin(0)));
			Vector3 nextPoint = Vector3.zero;
			float multiplier = 360f / circleDetail;

			Plane plane = new Plane((transform.position - target.position).normalized, target.position);

			for(var i = 0; i < circleDetail + 1; i++)
			{
				nextPoint.x = Mathf.Cos((i * multiplier) * Mathf.Deg2Rad);
				nextPoint.z = Mathf.Sin((i * multiplier) * Mathf.Deg2Rad);
				nextPoint.y = 0;
			
				nextPoint = origin + matrix.MultiplyPoint3x4(nextPoint);
			
				if(!depthTest || plane.GetSide(lastPoint))
				{
					resultsBuffer.Add(lastPoint);
					resultsBuffer.Add(nextPoint);
				}

				lastPoint = nextPoint;
			}
		}

		void DrawLines(List<Vector3> lines, Color color)
		{
			GL.Begin(GL.LINES);
			GL.Color(color);

			for(int i = 0; i < lines.Count; i += 2)
			{
				GL.Vertex(lines[i]);
				GL.Vertex(lines[i + 1]);
			}

			GL.End();
		}

		void DrawTriangles(List<Vector3> lines, Color color)
		{
			GL.Begin(GL.TRIANGLES);
			GL.Color(color);

			for(int i = 0; i < lines.Count; i += 3)
			{
				GL.Vertex(lines[i]);
				GL.Vertex(lines[i + 1]);
				GL.Vertex(lines[i + 2]);
			}

			GL.End();
		}

		void DrawSquares(List<Vector3> lines, Color color)
		{
			GL.Begin(GL.QUADS);
			GL.Color(color);

			for(int i = 0; i < lines.Count; i += 4)
			{
				GL.Vertex(lines[i]);
				GL.Vertex(lines[i + 1]);
				GL.Vertex(lines[i + 2]);
				GL.Vertex(lines[i + 3]);
			}

			GL.End();
		}

		void DrawCircles(List<Vector3> lines, Color color)
		{
			GL.Begin(GL.LINES);
			GL.Color(color);

			for(int i = 0; i < lines.Count; i += 2)
			{
				GL.Vertex(lines[i]);
				GL.Vertex(lines[i + 1]);
			}

			GL.End();
		}

		void SetMaterial()
		{
			if(lineMaterial == null)
			{
				lineMaterial = new Material(Shader.Find("Custom/Lines"));
				#region Shader code
				/*
				Shader "Custom/Lines"
				{
					SubShader
					{
						Pass
						{
							Blend SrcAlpha OneMinusSrcAlpha
							ZWrite Off
							ZTest Always
							Cull Off
							Fog { Mode Off }

							BindChannels
							{
								Bind "vertex", vertex
								Bind "color", color
							}
						}
					}
				}
				*/
				#endregion
			}
		}
	}
}
