﻿using System;
using UnityEngine;

namespace RuntimeGizmos
{
	public struct AxisInfo
	{
		public Vector3 xAxisEnd;
		public Vector3 yAxisEnd;
		public Vector3 zAxisEnd;
		public Vector3 xDirection;
		public Vector3 yDirection;
		public Vector3 zDirection;

		public void Set(Transform target, Vector3 pivot, float handleLength, TransformSpace space)
		{
			if(space == TransformSpace.Global)
			{
				xDirection = Vector3.right;
				yDirection = Vector3.up;
				zDirection = Vector3.forward;
			}
			else if(space == TransformSpace.Local)
			{
				xDirection = target.right;
				yDirection = target.up;
				zDirection = target.forward;
			}

			xAxisEnd = pivot + (xDirection * handleLength);
			yAxisEnd = pivot + (yDirection * handleLength);
			zAxisEnd = pivot + (zDirection * handleLength);
		}
	}
}
