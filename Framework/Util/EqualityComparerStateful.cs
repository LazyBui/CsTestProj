﻿using System;
using System.Collections.Generic;

namespace Test {
	/// <summary>
	/// Allows arbitrary equality comparison between two instances of the same class.
	/// </summary>
	/// <typeparam name="TValue">The type comparison will be done on.</typeparam>
	internal sealed class EqualityComparerStateful<TValue> : IEqualityComparer<TValue> {
		private Func<TValue, TValue, bool> mComparer = null;
		private Func<TValue, int> mHasher = null;

		public EqualityComparerStateful(Func<TValue, TValue, bool> pComparer, Func<TValue, int> pHasher = null) {
			if (pComparer == null) throw new ArgumentNullException("pComparer");
			if (pHasher == null) pHasher = v => 0;

			mComparer = pComparer;
			mHasher = pHasher;
		}

		public bool Equals(TValue x, TValue y) {
			return mComparer(x, y);
		}

		public int GetHashCode(TValue obj) {
			return mHasher(obj);
		}
	}
}