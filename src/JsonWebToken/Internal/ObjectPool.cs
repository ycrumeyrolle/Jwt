﻿// Copyright (c) 2018 Yann Crumeyrolle. All rights reserved.
// Licensed under the MIT license. See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace JsonWebToken.Internal
{
    // based on https://github.com/aspnet/Common/tree/master/src/Microsoft.Extensions.ObjectPool
    /// <summary>
    /// Represent a poolable <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ObjectPool<T> : IDisposable
        where T : class, IDisposable
    {
        private volatile bool _isDisposed;

        private readonly ObjectWrapper[] _items;
        private readonly PooledObjectFactory<T> _policy;
        private T _firstItem;

        /// <summary>
        /// Initializes a new instance of <see cref="ObjectPool{T}"/>.
        /// </summary>
        /// <param name="policy"></param>
        public ObjectPool(PooledObjectFactory<T> policy)
            : this(policy, Environment.ProcessorCount * 2)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ObjectPool{T}"/>.
        /// </summary>
        /// <param name="policy"></param>
        /// <param name="maximumRetained"></param>
        public ObjectPool(PooledObjectFactory<T> policy, int maximumRetained)
        {
            if (policy == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.policy);
            }

            _policy = policy;

            // -1 due to _firstItem
            _items = new ObjectWrapper[maximumRetained - 1];
        }

        /// <summary>
        /// Gets a <typeparamref name="T"/> from the pool.
        /// </summary>
        /// <returns></returns>
        public T Get()
        {
            if (_isDisposed)
            {
                ThrowObjectDisposedException();
            }

            var item = _firstItem;
            if (item == null || Interlocked.CompareExchange(ref _firstItem, null, item) != item)
            {
                var items = _items;
                for (var i = 0; i < items.Length; i++)
                {
                    item = items[i].Element;
                    if (item != null && Interlocked.CompareExchange(ref items[i].Element, null, item) == item)
                    {
                        return item;
                    }
                }

                item = _policy.Create();
            }

            return item;

            void ThrowObjectDisposedException()
            {
                ThrowHelper.ThrowObjectDisposedException(GetType());
            }
        }

        /// <summary>
        /// Returns a <typeparamref name="T"/> to the pool.
        /// </summary>
        /// <param name="pooledObject"></param>
        public void Return(T pooledObject)
        {
            // When the pool is disposed or the obj is not returned to the pool, dispose it
            if (_isDisposed || !ReturnCore(pooledObject))
            {
                DisposeItem(pooledObject);
            }
        }

        private bool ReturnCore(T obj)
        {
            bool returnedTooPool = false;

            if (_firstItem == null && Interlocked.CompareExchange(ref _firstItem, obj, null) == null)
            {
                returnedTooPool = true;
            }
            else
            {
                var items = _items;
                for (var i = 0; i < items.Length && !(returnedTooPool = Interlocked.CompareExchange(ref items[i].Element, obj, null) == null); i++)
                {
                }
            }

            return returnedTooPool;
        }

        /// <summary>
        /// Dispose the managed resources.
        /// </summary>
        public void Dispose()
        {
            _isDisposed = true;

            DisposeItem(_firstItem);
            _firstItem = null;

            ObjectWrapper[] items = _items;
            for (var i = 0; i < items.Length; i++)
            {
                DisposeItem(items[i].Element);
                items[i].Element = null;
            }
        }

        private void DisposeItem(T item)
        {
            if (item is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        // PERF: the struct wrapper avoids array-covariance-checks from the runtime when assigning to elements of the array.
        [DebuggerDisplay("{Element}")]
        private struct ObjectWrapper
        {
            public T Element;
        }
    }
}
