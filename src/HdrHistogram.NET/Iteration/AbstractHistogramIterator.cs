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


        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public bool MoveNext()
        {
            var canMove = this.HasNext();
            if (canMove)
            {
                this.Current = this.Next();
            }
            return canMove;
        }

        public void Reset()
        {
            this.ResetIterator(this.SourceHistogram);
        }

        

        public HistogramIterationValue Current { get; private set; }
        object IEnumerator.Current => this.Current;


        /**
         * Returns true if the iteration has more elements. (In other words, returns true if next would return an
         * element rather than throwing an exception.)
         *
         * @return true if the iterator has more elements.
         */
        public virtual bool HasNext()
        {
            if (this.SourceHistogram.GetTotalCount() != this._savedHistogramTotalRawCount)
            {
                throw new InvalidOperationException();
            }
            return (this.TotalCountToCurrentIndex < this.ArrayTotalCount);
        }

        /**
         * Returns the next element in the iteration.
         *
         * @return the {@link HistogramIterationValue} associated with the next element in the iteration.
         */
        public HistogramIterationValue Next()
        {
            // Move through the sub buckets and buckets until we hit the next reporting level:
            while (!this.ExhaustedSubBuckets())
            {
                this.CountAtThisValue = this.SourceHistogram.GetCountAt(this.CurrentBucketIndex, this.CurrentSubBucketIndex);
                if (this._freshSubBucket)
                {
                    // Don't add unless we've incremented since last bucket...
                    this.TotalCountToCurrentIndex += this.CountAtThisValue;
                    this._totalValueToCurrentIndex += this.CountAtThisValue * this.SourceHistogram.MedianEquivalentValue(this.CurrentValueAtIndex);
                    this._freshSubBucket = false;
                }
                if (this.ReachedIterationLevel())
                {
                    long valueIteratedTo = this.GetValueIteratedTo();
                    this._currentIterationValue.set(
                        valueIteratedTo,
                        this._prevValueIteratedTo,
                        this.CountAtThisValue,
                        (this.TotalCountToCurrentIndex - this._totalCountToPrevIndex),
                        this.TotalCountToCurrentIndex,
                        this._totalValueToCurrentIndex,
                        ((100.0 * this.TotalCountToCurrentIndex) / this.ArrayTotalCount),
                        this.GetPercentileIteratedTo());
                    this._prevValueIteratedTo = valueIteratedTo;
                    this._totalCountToPrevIndex = this.TotalCountToCurrentIndex;
                    // move the next iteration level forward:
                    this.IncrementIterationLevel();
                    if (this.SourceHistogram.GetTotalCount() != this._savedHistogramTotalRawCount)
                    {
                        throw new InvalidOperationException();
                    }
                    return this._currentIterationValue;
                }
                this.IncrementSubBucket();
            }
            // Should not reach here. But possible for overflowed histograms under certain conditions
            throw new ArgumentOutOfRangeException();
        }

        protected void ResetIterator(AbstractHistogram histogram)
        {
            this.SourceHistogram = histogram;
            this._savedHistogramTotalRawCount = histogram.GetTotalCount();
            this.ArrayTotalCount = histogram.GetTotalCount();
            this.CurrentBucketIndex = 0;
            this.CurrentSubBucketIndex = 0;
            this.CurrentValueAtIndex = 0;
            this._nextBucketIndex = 0;
            this._nextSubBucketIndex = 1;
            this.NextValueAtIndex = 1;
            this._prevValueIteratedTo = 0;
            this._totalCountToPrevIndex = 0;
            this.TotalCountToCurrentIndex = 0;
            this._totalValueToCurrentIndex = 0;
            this.CountAtThisValue = 0;
            this._freshSubBucket = true;
            if (this._currentIterationValue == null)
                this._currentIterationValue = new HistogramIterationValue();
            this._currentIterationValue.reset();
        }

        protected abstract void IncrementIterationLevel();

        protected abstract bool ReachedIterationLevel();

        protected virtual double GetPercentileIteratedTo()
        {
            return (100.0 * (double)this.TotalCountToCurrentIndex) / this.ArrayTotalCount;
        }

        protected virtual long GetValueIteratedTo()
        {
            return this.SourceHistogram.HighestEquivalentValue(this.CurrentValueAtIndex);
        }

        private bool ExhaustedSubBuckets()
        {
            return (this.CurrentBucketIndex >= this.SourceHistogram.BucketCount);
        }

        private void IncrementSubBucket()
        {
            this._freshSubBucket = true;
            // Take on the next index:
            this.CurrentBucketIndex = this._nextBucketIndex;
            this.CurrentSubBucketIndex = this._nextSubBucketIndex;
            this.CurrentValueAtIndex = this.NextValueAtIndex;
            // Figure out the next next index:
            this._nextSubBucketIndex++;
            if (this._nextSubBucketIndex >= this.SourceHistogram.SubBucketCount)
            {
                this._nextSubBucketIndex = this.SourceHistogram.SubBucketHalfCount;
                this._nextBucketIndex++;
            }
            this.NextValueAtIndex = this.SourceHistogram.ValueFromIndex(this._nextBucketIndex, this._nextSubBucketIndex);
        }
    }
}
