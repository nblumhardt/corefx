// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Pipes
{
    public abstract partial class PipeStream : Stream
    {
        private SafePipeHandle _handle;
        private bool _canRead;
        private bool _canWrite;
        private bool _isAsync;
        private bool _isMessageComplete;
        private bool _isFromExistingHandle;
        private bool _isHandleExposed;
        private PipeTransmissionMode _readMode;
        private PipeTransmissionMode _transmissionMode;
        private PipeDirection _pipeDirection;
        private int _outBufferSize;
        private PipeState _state;

        protected PipeStream(PipeDirection direction, int bufferSize)
        {
            if (direction < PipeDirection.In || direction > PipeDirection.InOut)
            {
                throw new ArgumentOutOfRangeException("direction", SR.ArgumentOutOfRange_DirectionModeInOutOrInOut);
            }
            if (bufferSize < 0)
            {
                throw new ArgumentOutOfRangeException("bufferSize", SR.ArgumentOutOfRange_NeedNonNegNum);
            }

            Init(direction, PipeTransmissionMode.Byte, bufferSize);
        }

        protected PipeStream(PipeDirection direction, PipeTransmissionMode transmissionMode, int outBufferSize)
        {
            if (direction < PipeDirection.In || direction > PipeDirection.InOut)
            {
                throw new ArgumentOutOfRangeException("direction", SR.ArgumentOutOfRange_DirectionModeInOutOrInOut);
            }
            if (transmissionMode < PipeTransmissionMode.Byte || transmissionMode > PipeTransmissionMode.Message)
            {
                throw new ArgumentOutOfRangeException("transmissionMode", SR.ArgumentOutOfRange_TransmissionModeByteOrMsg);
            }
            if (outBufferSize < 0)
            {
                throw new ArgumentOutOfRangeException("outBufferSize", SR.ArgumentOutOfRange_NeedNonNegNum);
            }

            Init(direction, transmissionMode, outBufferSize);
        }

        private void Init(PipeDirection direction, PipeTransmissionMode transmissionMode, int outBufferSize)
        {
            Debug.Assert(direction >= PipeDirection.In && direction <= PipeDirection.InOut, "invalid pipe direction");
            Debug.Assert(transmissionMode >= PipeTransmissionMode.Byte && transmissionMode <= PipeTransmissionMode.Message, "transmissionMode is out of range");
            Debug.Assert(outBufferSize >= 0, "outBufferSize is negative");

            // always defaults to this until overridden
            _readMode = transmissionMode;
            _transmissionMode = transmissionMode;

            _pipeDirection = direction;

            if ((_pipeDirection & PipeDirection.In) != 0)
            {
                _canRead = true;
            }
            if ((_pipeDirection & PipeDirection.Out) != 0)
            {
                _canWrite = true;
            }

            _outBufferSize = outBufferSize;

            // This should always default to true
            _isMessageComplete = true;

            _state = PipeState.WaitingToConnect;
        }

        // Once a PipeStream has a handle ready, it should call this method to set up the PipeStream.  If
        // the pipe is in a connected state already, it should also set the IsConnected (protected) property.
        // This method may also be called to uninitialize a handle, setting it to null.
        [SecuritySafeCritical]
        internal void InitializeHandle(SafePipeHandle handle, bool isExposed, bool isAsync)
        {
            if (isAsync && handle != null)
            {
                InitializeAsyncHandle(handle);
            }

            _handle = handle;
            _isAsync = isAsync;

            // track these separately; _isHandleExposed will get updated if accessed though the property
            _isHandleExposed = isExposed;
            _isFromExistingHandle = isExposed;
        }

        [SecurityCritical]
        public override int Read([In, Out] byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer", SR.ArgumentNull_Buffer);
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (buffer.Length - offset < count)
            {
                throw new ArgumentException(SR.Argument_InvalidOffLen);
            }
            if (!CanRead)
            {
                throw __Error.GetReadNotSupported();
            }

            CheckReadOperations();

            return ReadCore(buffer, offset, count);
        }

        [SecuritySafeCritical]
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer", SR.ArgumentNull_Buffer);
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_NeedNonNegNum);
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NeedNonNegNum);
            if (buffer.Length - offset < count)
                throw new ArgumentException(SR.Argument_InvalidOffLen);
            Contract.EndContractBlock();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<int>(cancellationToken);
            }

            CheckReadOperations();

            if (!_isAsync)
            {
                return base.ReadAsync(buffer, offset, count, cancellationToken);
            }

            return ReadAsyncCore(buffer, offset, count, cancellationToken);
        }

        [SecurityCritical]
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer", SR.ArgumentNull_Buffer);
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (buffer.Length - offset < count)
            {
                throw new ArgumentException(SR.Argument_InvalidOffLen);
            }
            if (!CanWrite)
            {
                throw __Error.GetWriteNotSupported();
            }
            CheckWriteOperations();

            WriteCore(buffer, offset, count);

            return;
        }

        [SecuritySafeCritical]
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer", SR.ArgumentNull_Buffer);
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_NeedNonNegNum);
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NeedNonNegNum);
            if (buffer.Length - offset < count)
                throw new ArgumentException(SR.Argument_InvalidOffLen);
            Contract.EndContractBlock();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<int>(cancellationToken);
            }

            CheckWriteOperations();

            if (!_isAsync)
            {
                return base.WriteAsync(buffer, offset, count, cancellationToken);
            }

            return WriteAsyncCore(buffer, offset, count, cancellationToken);
        }


        [ThreadStatic]
        private static byte[] t_singleByteArray;

        private static byte[] SingleByteArray
        {
            get { return t_singleByteArray ?? (t_singleByteArray = new byte[1]); }
        }

        // Reads a byte from the pipe stream.  Returns the byte cast to an int
        // or -1 if the connection has been broken.
        [SecurityCritical]
        public override int ReadByte()
        {
            CheckReadOperations();
            if (!CanRead)
            {
                throw __Error.GetReadNotSupported();
            }

            byte[] buffer = SingleByteArray;
            int n = ReadCore(buffer, 0, 1);

            if (n == 0) { return -1; }
            else return (int)buffer[0];
        }

        [SecurityCritical]
        public override void WriteByte(byte value)
        {
            CheckWriteOperations();
            if (!CanWrite)
            {
                throw __Error.GetWriteNotSupported();
            }

            byte[] buffer = SingleByteArray;
            buffer[0] = value;
            WriteCore(buffer, 0, 1);
        }

        // Does nothing on PipeStreams.  We cannot call Interop.FlushFileBuffers here because we can deadlock
        // if the other end of the pipe is no longer interested in reading from the pipe. 
        [SecurityCritical]
        public override void Flush()
        {
            CheckWriteOperations();
            if (!CanWrite)
            {
                throw __Error.GetWriteNotSupported();
            }
        }

        [SecurityCritical]
        protected override void Dispose(bool disposing)
        {
            try
            {
                // Nothing will be done differently based on whether we are 
                // disposing vs. finalizing.  
                if (_handle != null && !_handle.IsClosed)
                {
                    _handle.Dispose();
                }

                UninitializeAsyncHandle();
            }
            finally
            {
                base.Dispose(disposing);
            }

            _state = PipeState.Closed;
        }

        // ********************** Public Properties *********************** //

        // APIs use coarser definition of connected, but these map to internal 
        // Connected/Disconnected states. Note that setter is protected; only
        // intended to be called by custom PipeStream concrete children
        public bool IsConnected
        {
            get
            {
                return State == PipeState.Connected;
            }
            protected set
            {
                _state = (value) ? PipeState.Connected : PipeState.Disconnected;
            }
        }

        public bool IsAsync
        {
            get { return _isAsync; }
        }

        // Set by the most recent call to Read or EndRead.  Will be false if there are more buffer in the
        // message, otherwise it is set to true. 
        public bool IsMessageComplete
        {
            [SecurityCritical]
            [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands", Justification = "Security model of pipes: demand at creation but no subsequent demands")]
            get
            {
                // omitting pipe broken exception to allow reader to finish getting message
                if (_state == PipeState.WaitingToConnect)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_PipeNotYetConnected);
                }
                if (_state == PipeState.Disconnected)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_PipeDisconnected);
                }
                if (CheckOperationsRequiresSetHandle && _handle == null)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_PipeHandleNotSet);
                }

                if ((_state == PipeState.Closed) || (_handle != null && _handle.IsClosed))
                {
                    throw __Error.GetPipeNotOpen();
                }
                // don't need to check transmission mode; just care about read mode. Always use
                // cached mode; otherwise could throw for valid message when other side is shutting down
                if (_readMode != PipeTransmissionMode.Message)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_PipeReadModeNotMessage);
                }

                return _isMessageComplete;
            }
        }

        public SafePipeHandle SafePipeHandle
        {
            [SecurityCritical]
            [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands", Justification = "Security model of pipes: demand at creation but no subsequent demands")]
            get
            {
                if (_handle == null)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_PipeHandleNotSet);
                }
                if (_handle.IsClosed)
                {
                    throw __Error.GetPipeNotOpen();
                }

                _isHandleExposed = true;
                return _handle;
            }
        }

        internal SafePipeHandle InternalHandle
        {
            [SecurityCritical]
            get
            {
                return _handle;
            }
        }

        internal bool IsHandleExposed
        {
            get
            {
                return _isHandleExposed;
            }
        }

        public override bool CanRead
        {
            [Pure]
            get
            {
                return _canRead;
            }
        }

        public override bool CanWrite
        {
            [Pure]
            get
            {
                return _canWrite;
            }
        }

        public override bool CanSeek
        {
            [Pure]
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                throw __Error.GetSeekNotSupported();
            }
        }

        public override long Position
        {
            get
            {
                throw __Error.GetSeekNotSupported();
            }
            set
            {
                throw __Error.GetSeekNotSupported();
            }
        }

        public override void SetLength(long value)
        {
            throw __Error.GetSeekNotSupported();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw __Error.GetSeekNotSupported();
        }

        // anonymous pipe ends and named pipe server can get/set properties when broken 
        // or connected. Named client overrides
        [SecurityCritical]
        internal virtual void CheckPipePropertyOperations()
        {
            if (CheckOperationsRequiresSetHandle && _handle == null)
            {
                throw new InvalidOperationException(SR.InvalidOperation_PipeHandleNotSet);
            }

            // these throw object disposed
            if ((_state == PipeState.Closed) || (_handle != null && _handle.IsClosed))
            {
                throw __Error.GetPipeNotOpen();
            }
        }

        // Reads can be done in Connected and Broken. In the latter,
        // read returns 0 bytes
        [SecurityCritical]
        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands", Justification = "Consistent with security model")]
        internal void CheckReadOperations()
        {
            // Invalid operation
            if (_state == PipeState.WaitingToConnect)
            {
                throw new InvalidOperationException(SR.InvalidOperation_PipeNotYetConnected);
            }
            if (_state == PipeState.Disconnected)
            {
                throw new InvalidOperationException(SR.InvalidOperation_PipeDisconnected);
            }
            if (CheckOperationsRequiresSetHandle && _handle == null)
            {
                throw new InvalidOperationException(SR.InvalidOperation_PipeHandleNotSet);
            }

            // these throw object disposed
            if ((_state == PipeState.Closed) || (_handle != null && _handle.IsClosed))
            {
                throw __Error.GetPipeNotOpen();
            }
        }

        // Writes can only be done in connected state
        [SecurityCritical]
        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands", Justification = "Consistent with security model")]
        internal void CheckWriteOperations()
        {
            // Invalid operation
            if (_state == PipeState.WaitingToConnect)
            {
                throw new InvalidOperationException(SR.InvalidOperation_PipeNotYetConnected);
            }
            if (_state == PipeState.Disconnected)
            {
                throw new InvalidOperationException(SR.InvalidOperation_PipeDisconnected);
            }
            if (CheckOperationsRequiresSetHandle && _handle == null)
            {
                throw new InvalidOperationException(SR.InvalidOperation_PipeHandleNotSet);
            }

            // IOException
            if (_state == PipeState.Broken)
            {
                throw new IOException(SR.IO_PipeBroken);
            }

            // these throw object disposed
            if ((_state == PipeState.Closed) || (_handle != null && _handle.IsClosed))
            {
                throw __Error.GetPipeNotOpen();
            }
        }

        internal PipeState State
        {
            get
            {
                return _state;
            }
            set
            {
                _state = value;
            }
        }
    }
}


