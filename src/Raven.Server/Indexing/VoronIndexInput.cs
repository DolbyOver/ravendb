using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Lucene.Net.Store;
using Raven.Abstractions.Extensions;
using Sparrow;
using Voron.Impl;

namespace Raven.Server.Indexing
{
    public unsafe class VoronIndexInput : IndexInput
    {

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private MmapStream _stream;
        private bool _isOriginal = true;

        private readonly long _originalTransactionId;
        private readonly ThreadLocal<Transaction> _currentTransaction;

        private readonly string _name;

        private byte* _basePtr;
        private int _size;

        public VoronIndexInput(ThreadLocal<Transaction> transaction, string name)
        {
            _name = name;
            _originalTransactionId = transaction.Value.LowLevelTransaction.Id;
            _currentTransaction = transaction;

            OpenInternal();
        }

        private void OpenInternal()
        {
            var fileTree = _currentTransaction.Value.ReadTree(_name);

            if (fileTree == null)
                throw new FileNotFoundException("Could not find index input", _name);

            var readResult = fileTree.Read(VoronIndexOutput.DataKey);
            if (readResult == null)
                throw new InvalidDataException($"Could not find data of file {_name}");

            _basePtr = readResult.Reader.Base;
            _size = readResult.Reader.Length;
            _stream = new MmapStream(readResult.Reader.Base, readResult.Reader.Length);
        }

        public override object Clone()
        {
            AssertNotDisposed();

            var clone = (VoronIndexInput)base.Clone();
            GC.SuppressFinalize(clone);
            clone._isOriginal = false;

            if (clone._originalTransactionId != clone._currentTransaction.Value.LowLevelTransaction.Id)
                clone.OpenInternal();
            else
            {
                clone._stream = new MmapStream(clone._basePtr, clone._size)
                {
                    Position = _stream.Position
                };
            }

            return clone;
        }

        public override byte ReadByte()
        {
            AssertNotDisposed();

            var readByte = _stream.ReadByte();
            if (readByte == -1)
                throw new EndOfStreamException();
            return (byte)readByte;
        }

        public override void ReadBytes(byte[] b, int offset, int len)
        {
            AssertNotDisposed();

            _stream.ReadEntireBlock(b, offset, len);
        }

        public override void Seek(long pos)
        {
            AssertNotDisposed();

            _stream.Seek(pos, SeekOrigin.Begin);
        }

        protected override void Dispose(bool disposing)
        {
            GC.SuppressFinalize(this);

            if (_isOriginal == false)
                return;

            _cts.Cancel();
        }

        public override long Length()
        {
            return _stream.Length;
        }

        public override long FilePointer
        {
            get
            {
                AssertNotDisposed();

                return _stream.Position;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertNotDisposed()
        {
            if (_cts.IsCancellationRequested)
                throw new ObjectDisposedException("VoronIndexInput");
        }

        private class MmapStream : Stream
        {
            private readonly byte* ptr;
            private readonly long len;
            private long pos;

            public MmapStream(byte* ptr, long len)
            {
                this.ptr = ptr;
                this.len = len;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        Position = offset;
                        break;
                    case SeekOrigin.Current:
                        Position += offset;
                        break;
                    case SeekOrigin.End:
                        Position = len + offset;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("origin", origin, null);
                }
                return Position;
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override int ReadByte()
            {
                if (Position == len)
                    return -1;
                return ptr[pos++];

            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (pos == len)
                    return 0;
                if (count > len - pos)
                {
                    count = (int)(len - pos);
                }
                fixed (byte* dst = buffer)
                {
                    Memory.CopyInline(dst + offset, ptr + pos, count);
                }
                pos += count;
                return count;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override bool CanRead
            {
                get { return true; }
            }
            public override bool CanSeek
            {
                get { return true; }
            }
            public override bool CanWrite
            {
                get { return false; }
            }
            public override long Length
            {
                get { return len; }
            }
            public override long Position { get { return pos; } set { pos = value; } }
        }
    }
}