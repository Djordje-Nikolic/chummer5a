/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Chummer
{
    /// <summary>
    /// A version of Dictionary that is paired with a ReaderWriterLock.
    /// In theory, this dictionary can be faster in serial contexts than ConcurrentDictionary.
    /// Because ReadWriterLock allows parallel reads and only locks out writes, it's also faster than using a simple setup with the lock keyword.
    /// However, for mass parallel writes, use ConcurrentDictionary instead because locking the entire dictionary when accessing keys is not good for performance.
    /// </summary>
    /// <typeparam name="TKey">Key to use for the dictionary.</typeparam>
    /// <typeparam name="TValue">Values to use for the dictionary.</typeparam>
    public class LockingDictionary<TKey, TValue> : IAsyncDictionary<TKey, TValue>, IAsyncReadOnlyDictionary<TKey, TValue>, IAsyncProducerConsumerCollection<KeyValuePair<TKey, TValue>>, IHasLockObject, ISerializable, IDeserializationCallback
    {
        private readonly Dictionary<TKey, TValue> _dicData;
        public AsyncFriendlyReaderWriterLock LockObject { get; } = new AsyncFriendlyReaderWriterLock();

        public LockingDictionary()
        {
            _dicData = new Dictionary<TKey, TValue>();
        }

        public LockingDictionary(int capacity)
        {
            _dicData = new Dictionary<TKey, TValue>(capacity);
        }

        public LockingDictionary(IDictionary<TKey, TValue> dictionary)
        {
            _dicData = new Dictionary<TKey, TValue>(dictionary);
        }

        public LockingDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
        {
            _dicData = new Dictionary<TKey, TValue>(dictionary, comparer);
        }

        public LockingDictionary(IEqualityComparer<TKey> comparer)
        {
            _dicData = new Dictionary<TKey, TValue>(comparer);
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            LockingEnumerator<KeyValuePair<TKey, TValue>> objReturn = LockingEnumerator<KeyValuePair<TKey, TValue>>.Get(this);
            objReturn.SetEnumerator(_dicData.GetEnumerator());
            return objReturn;
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            LockingDictionaryEnumerator objReturn = LockingDictionaryEnumerator.Get(this);
            objReturn.SetEnumerator(_dicData.GetEnumerator());
            return objReturn;
        }

        public async ValueTask<IEnumerator<KeyValuePair<TKey, TValue>>> GetEnumeratorAsync(CancellationToken token = default)
        {
            LockingEnumerator<KeyValuePair<TKey, TValue>> objReturn = await LockingEnumerator<KeyValuePair<TKey, TValue>>.GetAsync(this, token).ConfigureAwait(false);
            objReturn.SetEnumerator(_dicData.GetEnumerator());
            return objReturn;
        }

        /// <inheritdoc />
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            using (LockObject.EnterWriteLock())
                _dicData.Add(item.Key, item.Value);
        }

        public async ValueTask AddAsync(KeyValuePair<TKey, TValue> item, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                _dicData.Add(item.Key, item.Value);
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        public ValueTask AddAsync(object key, object value, CancellationToken token = default)
        {
            return AddAsync((TKey)key, (TValue)value, token);
        }

        /// <inheritdoc cref="IDictionary{TKey, TValue}.Clear" />
        public void Clear()
        {
            using (LockObject.EnterWriteLock())
                _dicData.Clear();
        }

        /// <inheritdoc cref="IDictionary{TKey, TValue}.Clear" />
        public async ValueTask ClearAsync(CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                _dicData.Clear();
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            using (EnterReadLock.Enter(LockObject))
                return _dicData.Contains(item);
        }

        public async ValueTask<bool> ContainsAsync(KeyValuePair<TKey, TValue> item, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _dicData.Contains(item);
        }

        /// <inheritdoc cref="ICollection.CopyTo" />
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                foreach (KeyValuePair<TKey, TValue> kvpItem in _dicData)
                {
                    array[arrayIndex] = kvpItem;
                    ++arrayIndex;
                }
            }
        }

        /// <inheritdoc />
        public void CopyTo(Array array, int index)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                foreach (KeyValuePair<TKey, TValue> kvpItem in _dicData)
                {
                    array.SetValue(kvpItem, index);
                    ++index;
                }
            }
        }

        /// <inheritdoc cref="ICollection.CopyTo" />
        public async ValueTask CopyToAsync(KeyValuePair<TKey, TValue>[] array, int index, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                foreach (KeyValuePair<TKey, TValue> kvpItem in _dicData)
                {
                    array[index] = kvpItem;
                    ++index;
                }
            }
        }

        /// <inheritdoc cref="ICollection.CopyTo" />
        public async ValueTask CopyToAsync(Array array, int index, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                foreach (KeyValuePair<TKey, TValue> kvpItem in _dicData)
                {
                    array.SetValue(kvpItem, index);
                    ++index;
                }
            }
        }

        /// <inheritdoc />
        public KeyValuePair<TKey, TValue>[] ToArray()
        {
            using (EnterReadLock.Enter(LockObject))
            {
                KeyValuePair<TKey, TValue>[] akvpReturn = new KeyValuePair<TKey, TValue>[_dicData.Count];
                int i = 0;
                foreach (KeyValuePair<TKey, TValue> kvpLoop in _dicData)
                {
                    akvpReturn[i] = kvpLoop;
                    ++i;
                }
                return akvpReturn;
            }
        }

        /// <inheritdoc />
        public async ValueTask<Tuple<bool, KeyValuePair<TKey, TValue>>> TryTakeAsync(CancellationToken token = default)
        {
            bool blnTakeSuccessful = false;
            TKey objKeyToTake = default;
            TValue objValue = default;
            // Immediately enter a write lock to prevent attempted reads until we have either taken the item we want to take or failed to do so
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                if (_dicData.Count > 0)
                {
                    // FIFO to be compliant with how the default for BlockingCollection<T> is ConcurrentQueue
                    objKeyToTake = _dicData.Keys.First();
                    if (_dicData.TryGetValue(objKeyToTake, out objValue))
                    {
                        blnTakeSuccessful = _dicData.Remove(objKeyToTake);
                    }
                }
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }

            return blnTakeSuccessful
                ? new Tuple<bool, KeyValuePair<TKey, TValue>>(
                    true, new KeyValuePair<TKey, TValue>(objKeyToTake, objValue))
                : new Tuple<bool, KeyValuePair<TKey, TValue>>(false, default);
        }

        public async ValueTask<KeyValuePair<TKey, TValue>[]> ToArrayAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                KeyValuePair<TKey, TValue>[] akvpReturn = new KeyValuePair<TKey, TValue>[_dicData.Count];
                int i = 0;
                foreach (KeyValuePair<TKey, TValue> kvpLoop in _dicData)
                {
                    akvpReturn[i] = kvpLoop;
                    ++i;
                }
                return akvpReturn;
            }
        }

        /// <inheritdoc />
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            // Immediately enter a write lock to prevent attempted reads until we have either removed the item we want to remove or failed to do so
            using (LockObject.EnterWriteLock())
            {
                return _dicData.TryGetValue(item.Key, out TValue objValue) && objValue.Equals(item.Value)
                                                                           && _dicData.Remove(item.Key);
            }
        }

        /// <inheritdoc />
        public bool Remove(TKey key)
        {
            using (LockObject.EnterWriteLock())
                return _dicData.Remove(key);
        }

        public async ValueTask<bool> RemoveAsync(KeyValuePair<TKey, TValue> item, CancellationToken token = default)
        {
            // Immediately enter a write lock to prevent attempted reads until we have either removed the item we want to remove or failed to do so
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                return _dicData.TryGetValue(item.Key, out TValue objValue) && objValue.Equals(item.Value)
                                                                           && _dicData.Remove(item.Key);
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        public async ValueTask<bool> RemoveAsync(TKey key, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                return _dicData.Remove(key);
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        public ValueTask<bool> RemoveAsync(object key, CancellationToken token = default)
        {
            return RemoveAsync((TKey)key, token);
        }

        public bool TryRemove(TKey key, out TValue value)
        {
            // Immediately enter a write lock to prevent attempted reads until we have either removed the item we want to remove or failed to do so
            using (LockObject.EnterWriteLock())
                return _dicData.TryGetValue(key, out value) && _dicData.Remove(key);
        }

        public async ValueTask<Tuple<bool, TValue>> TryRemoveAsync(TKey key, CancellationToken token = default)
        {
            // Immediately enter a write lock to prevent attempted reads until we have either removed the item we want to remove or failed to do so
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                bool blnSuccess = _dicData.TryGetValue(key, out TValue value) && _dicData.Remove(key);
                return new Tuple<bool, TValue>(blnSuccess, value);
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public bool TryTake(out KeyValuePair<TKey, TValue> item)
        {
            bool blnTakeSuccessful = false;
            TKey objKeyToTake = default;
            TValue objValue = default;
            // Immediately enter a write lock to prevent attempted reads until we have either taken the item we want to take or failed to do so
            using (LockObject.EnterWriteLock())
            {
                if (_dicData.Count > 0)
                {
                    // FIFO to be compliant with how the default for BlockingCollection<T> is ConcurrentQueue
                    objKeyToTake = _dicData.Keys.First();
                    if (_dicData.TryGetValue(objKeyToTake, out objValue))
                    {
                        blnTakeSuccessful = _dicData.Remove(objKeyToTake);
                    }
                }
            }

            if (blnTakeSuccessful)
            {
                item = new KeyValuePair<TKey, TValue>(objKeyToTake, objValue);
                return true;
            }
            item = default;
            return false;
        }

        /// <inheritdoc cref="IDictionary{TKey, TValue}.Count" />
        public int Count
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _dicData.Count;
            }
        }

        public async ValueTask<int> GetCountAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _dicData.Count;
        }

        /// <inheritdoc />
        public object SyncRoot => LockObject;

        /// <inheritdoc />
        public bool IsSynchronized => true;

        /// <inheritdoc cref="IDictionary{TKey, TValue}.IsReadOnly" />
        public bool IsReadOnly => false;

        /// <inheritdoc cref="IDictionary{TKey, TValue}.ContainsKey" />
        public bool ContainsKey(TKey key)
        {
            using (EnterReadLock.Enter(LockObject))
                return _dicData.ContainsKey(key);
        }

        /// <inheritdoc cref="IDictionary{TKey, TValue}.ContainsKey" />
        public async ValueTask<bool> ContainsKeyAsync(TKey key, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _dicData.ContainsKey(key);
        }

        /// <inheritdoc />
        public void Add(TKey key, TValue value)
        {
            using (LockObject.EnterWriteLock())
                _dicData.Add(key, value);
        }

        public async ValueTask AddAsync(TKey key, TValue value, CancellationToken token = default)
        {
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                _dicData.Add(key, value);
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        public bool TryAdd(TKey key, TValue value)
        {
            // Immediately enter a write lock to prevent attempted reads until we have either added the item we want to add or failed to do so
            using (LockObject.EnterWriteLock())
            {
                if (_dicData.ContainsKey(key))
                    return false;
                _dicData.Add(key, value);
            }
            return true;
        }

        public async ValueTask<bool> TryAddAsync(TKey key, TValue value, CancellationToken token = default)
        {
            // Immediately enter a write lock to prevent attempted reads until we have either added the item we want to add or failed to do so
            IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_dicData.ContainsKey(key))
                    return false;
                _dicData.Add(key, value);
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
            return true;
        }

        /// <inheritdoc />
        public bool TryAdd(KeyValuePair<TKey, TValue> item)
        {
            return TryAdd(item.Key, item.Value);
        }

        public ValueTask<bool> TryAddAsync(KeyValuePair<TKey, TValue> item, CancellationToken token = default)
        {
            return TryAddAsync(item.Key, item.Value, token);
        }

        /// <summary>
        /// Uses the specified functions to add a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <returns>The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public TValue AddOrUpdate(TKey key, Func<TKey, TValue> addValueFactory,
                                  Func<TKey, TValue, TValue> updateValueFactory)
        {
            TValue objReturn;
            using (EnterReadLock.Enter(LockObject))
            {
                if (_dicData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = updateValueFactory(key, objExistingValue);
                    using (LockObject.EnterWriteLock())
                        _dicData[key] = objReturn;
                }
                else
                {
                    objReturn = addValueFactory(key);
                    using (LockObject.EnterWriteLock())
                        _dicData.Add(key, objReturn);
                }
            }
            return objReturn;
        }

        /// <summary>
        /// Adds a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValue">The value to be added for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <returns>The new value for the key. This will be either be addValue (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory)
        {
            using (EnterReadLock.Enter(LockObject))
            {
                if (_dicData.TryGetValue(key, out TValue objExistingValue))
                {
                    TValue objNewValue = updateValueFactory(key, objExistingValue);
                    using (LockObject.EnterWriteLock())
                        _dicData[key] = objNewValue;
                    return objNewValue;
                }
                using (LockObject.EnterWriteLock())
                    _dicData.Add(key, addValue);
                return addValue;
            }
        }

        /// <summary>
        /// Uses the specified functions to add a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be the result of addValueFactory (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public async ValueTask<TValue> AddOrUpdateAsync(TKey key, Func<TKey, TValue> addValueFactory,
                                                        Func<TKey, TValue, TValue> updateValueFactory, CancellationToken token = default)
        {
            TValue objReturn;
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicData.TryGetValue(key, out TValue objExistingValue))
                {
                    objReturn = updateValueFactory(key, objExistingValue);
                    IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        _dicData[key] = objReturn;
                    }
                    finally
                    {
                        await objLocker.DisposeAsync().ConfigureAwait(false);
                    }
                }
                else
                {
                    objReturn = addValueFactory(key);
                    IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        _dicData.Add(key, objReturn);
                    }
                    finally
                    {
                        await objLocker.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            return objReturn;
        }

        /// <summary>
        /// Adds a key/value pair to the dictionary if the key does not already exist, or to update a key/value pair in the dictionary if the key already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValue">The value to be added for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>The new value for the key. This will be either be addValue (if the key was absent) or the result of updateValueFactory (if the key was present).</returns>
        public async ValueTask<TValue> AddOrUpdateAsync(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                IAsyncDisposable objLocker;
                if (_dicData.TryGetValue(key, out TValue objExistingValue))
                {
                    TValue objNewValue = updateValueFactory(key, objExistingValue);
                    objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        _dicData[key] = objNewValue;
                    }
                    finally
                    {
                        await objLocker.DisposeAsync().ConfigureAwait(false);
                    }
                    return objNewValue;
                }
                objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    _dicData.Add(key, addValue);
                }
                finally
                {
                    await objLocker.DisposeAsync().ConfigureAwait(false);
                }
                return addValue;
            }
        }

        /// <inheritdoc cref="IDictionary{TKey, TValue}.TryGetValue" />
        public bool TryGetValue(TKey key, out TValue value)
        {
            using (EnterReadLock.Enter(LockObject))
                return _dicData.TryGetValue(key, out value);
        }

        /// <inheritdoc cref="IDictionary{TKey, TValue}.TryGetValue" />
        public async ValueTask<Tuple<bool, TValue>> TryGetValueAsync(TKey key, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                bool blnSuccess = _dicData.TryGetValue(key, out TValue value);
                return new Tuple<bool, TValue>(blnSuccess, value);
            }
        }

        // ReSharper disable once InheritdocInvalidUsage
        /// <inheritdoc />
        public TValue this[TKey key]
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _dicData[key];
            }
            set
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    if (_dicData.TryGetValue(key, out TValue objValue) && objValue.Equals(value))
                        return;
                    using (LockObject.EnterWriteLock())
                        _dicData[key] = value;
                }
            }
        }

        public async ValueTask<TValue> GetValueAtAsync(TKey key, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _dicData[key];
        }

        public async ValueTask SetValueAtAsync(TKey key, TValue value, CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
            {
                if (_dicData.TryGetValue(key, out TValue objValue) && objValue.Equals(value))
                    return;
                IAsyncDisposable objLocker = await LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                try
                {
                    _dicData[key] = value;
                }
                finally
                {
                    await objLocker.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    // This construction makes sure we hold onto the lock until enumeration is done
                    foreach (TKey objKey in _dicData.Keys)
                        yield return objKey;
                }
            }
        }

        /// <inheritdoc />
        public ICollection<TKey> Keys
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _dicData.Keys;
            }
        }

        public async ValueTask<ICollection<TKey>> GetKeysAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _dicData.Keys;
        }

        public async ValueTask<IReadOnlyCollection<TKey>> GetReadOnlyKeysAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _dicData.Keys;
        }

        /// <inheritdoc />
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                {
                    // This construction makes sure we hold onto the lock until enumeration is done
                    foreach (TValue objValue in _dicData.Values)
                        yield return objValue;
                }
            }
        }

        /// <inheritdoc />
        public ICollection<TValue> Values
        {
            get
            {
                using (EnterReadLock.Enter(LockObject))
                    return _dicData.Values;
            }
        }

        public async ValueTask<ICollection<TValue>> GetValuesAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _dicData.Values;
        }

        public async ValueTask<IReadOnlyCollection<TValue>> GetReadOnlyValuesAsync(CancellationToken token = default)
        {
            using (await EnterReadLock.EnterAsync(LockObject, token).ConfigureAwait(false))
                return _dicData.Values;
        }

        private int _intIsDisposed;

        public bool IsDisposed => _intIsDisposed > 0;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Interlocked.CompareExchange(ref _intIsDisposed, 1, 0) > 0)
                    return;
                LockObject.Dispose();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsync(bool disposing)
        {
            if (disposing)
            {
                if (Interlocked.CompareExchange(ref _intIsDisposed, 1, 0) > 0)
                    return;
                await LockObject.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await DisposeAsync(true).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            using (EnterReadLock.Enter(LockObject))
                _dicData.GetObjectData(info, context);
        }

        /// <inheritdoc />
        public void OnDeserialization(object sender)
        {
            using (EnterReadLock.Enter(LockObject))
                _dicData.OnDeserialization(sender);
        }
    }
}
