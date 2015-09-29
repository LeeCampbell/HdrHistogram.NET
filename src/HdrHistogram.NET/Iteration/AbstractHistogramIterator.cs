/*
 * Written by Matt Warren, and released to the public domain,
 * as explained at
 * http://creativecommons.org/publicdomain/zero/1.0/
 *
 * This is a .NET port of the original Java version, which was written by
 * Gil Tene as described in
 * https://github.com/HdrHistogram/HdrHistogram
 */

using System;
using System.Collections;
using System.Collections.Generic;

namespace HdrHistogram.NET.Iteration
{
    public abstract class AbstractHistogramIterator : IEnumerator<HistogramIterationValue>
    {
        private long _savedHistogramTotalRawCount;
        private int _nextBucketIndex;
        private int _nextSubBucketIndex;
        private long _prevValueIteratedTo;
        private long _totalCountToPrevIndex;
        private long _totalValueToCurrentIndex;
        private bool _freshSubBucket;
        private HistogramIterationValue _currentIterationValue;

        protected AbstractHistogram SourceHistogram { get; private set; }
        protected int CurrentBucketIndex { get; private set; }
        protected int CurrentSubBucketIndex { get; private set; }
        protected long CurrentValueAtIndex { get; private set; }
        protected long NextValueAtIndex { get; private set; }
        protected long TotalCountToCurrentIndex { get; private set; }
        protected long ArrayTotalCount { get; private set; }
        protected long CountAtThisValue { get; private set; }

        public HistogramIterationValue Current { get; private set; }
        object IEnumerator.Current => Current;
        
        public bool MoveNext()
        {
            var canMove = HasNext();
            if (canMove)
            {
                Current = Next();
            }
            return canMove;
        }

        public void Reset()
        {
            ResetIterator(SourceHistogram);
        }

        /// <summary>
        ///  Returns <c>true</c> if the iteration has more elements. (In other words, returns true if next would return an element rather than throwing an exception.)
        /// </summary>
        /// <returns><c>true</c> if the iterator has more elements.</returns>
        public virtual bool HasNext()
        {
            if (SourceHistogram.GetTotalCount() != _savedHistogramTotalRawCount)
            {
                throw new InvalidOperationException();
            }
            return (TotalCountToCurrentIndex < ArrayTotalCount);
        }

        /// <summary>
        /// Returns the next element in the iteration.
        /// </summary>
        /// <returns>the <see cref="HistogramIterationValue"/> associated with the next element in the iteration.</returns>
        public HistogramIterationValue Next()
        {
            // Move through the sub buckets and buckets until we hit the next reporting level:
            while (!ExhaustedSubBuckets())
            {
                CountAtThisValue = SourceHistogram.GetCountAt(CurrentBucketIndex, CurrentSubBucketIndex);
                if (_freshSubBucket)
                {
                    // Don't add unless we've incremented since last bucket...
                    TotalCountToCurrentIndex += CountAtThisValue;
                    _totalValueToCurrentIndex += CountAtThisValue * SourceHistogram.MedianEquivalentValue(CurrentValueAtIndex);
                    _freshSubBucket = false;
                }
                if (ReachedIterationLevel())
                {
                    long valueIteratedTo = GetValueIteratedTo();
                    _currentIterationValue.set(
                        valueIteratedTo,
                        _prevValueIteratedTo,
                        CountAtThisValue,
                        (TotalCountToCurrentIndex - _totalCountToPrevIndex),
                        TotalCountToCurrentIndex,
                        _totalValueToCurrentIndex,
                        ((100.0 * TotalCountToCurrentIndex) / ArrayTotalCount),
                        GetPercentileIteratedTo());
                    _prevValueIteratedTo = valueIteratedTo;
                    _totalCountToPrevIndex = TotalCountToCurrentIndex;
                    // move the next iteration level forward:
                    IncrementIterationLevel();
                    if (SourceHistogram.GetTotalCount() != _savedHistogramTotalRawCount)
                    {
                        throw new InvalidOperationException();
                    }
                    return _currentIterationValue;
                }
                IncrementSubBucket();
            }
            // Should not reach here. But possible for overflowed histograms under certain conditions
            throw new ArgumentOutOfRangeException();
        }

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        protected virtual void ResetIterator(AbstractHistogram histogram)
        {
            SourceHistogram = histogram;
            _savedHistogramTotalRawCount = histogram.GetTotalCount();
            ArrayTotalCount = histogram.GetTotalCount();
            CurrentBucketIndex = 0;
            CurrentSubBucketIndex = 0;
            CurrentValueAtIndex = 0;
            _nextBucketIndex = 0;
            _nextSubBucketIndex = 1;
            NextValueAtIndex = 1;
            _prevValueIteratedTo = 0;
            _totalCountToPrevIndex = 0;
            TotalCountToCurrentIndex = 0;
            _totalValueToCurrentIndex = 0;
            CountAtThisValue = 0;
            _freshSubBucket = true;
            if (_currentIterationValue == null)
                _currentIterationValue = new HistogramIterationValue();
            _currentIterationValue.reset();
        }

        protected abstract void IncrementIterationLevel();

        protected abstract bool ReachedIterationLevel();

        protected virtual double GetPercentileIteratedTo()
        {
            return (100.0 * TotalCountToCurrentIndex) / ArrayTotalCount;
        }

        protected virtual long GetValueIteratedTo()
        {
            return SourceHistogram.HighestEquivalentValue(CurrentValueAtIndex);
        }

        private bool ExhaustedSubBuckets()
        {
            return (CurrentBucketIndex >= SourceHistogram.BucketCount);
        }

        private void IncrementSubBucket()
        {
            _freshSubBucket = true;
            // Take on the next index:
            CurrentBucketIndex = _nextBucketIndex;
            CurrentSubBucketIndex = _nextSubBucketIndex;
            CurrentValueAtIndex = NextValueAtIndex;
            // Figure out the next next index:
            _nextSubBucketIndex++;
            if (_nextSubBucketIndex >= SourceHistogram.SubBucketCount)
            {
                _nextSubBucketIndex = SourceHistogram.SubBucketHalfCount;
                _nextBucketIndex++;
            }
            NextValueAtIndex = SourceHistogram.ValueFromIndex(_nextBucketIndex, _nextSubBucketIndex);
        }
    }
}
