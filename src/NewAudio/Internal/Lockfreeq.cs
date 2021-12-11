using System;
using System.Collections.Generic;
using System.Threading;

namespace VL.NewAudio.Internal
{
    public class MSQueue<T>
    {
        private class node_t
        {
            public T value;
            public pointer_t next;

            /// <summary>
            /// default constructor
            /// </summary>
            public node_t()
            {
            }
        }

        private struct pointer_t
        {
            public long count;
            public node_t ptr;

            /// <summary>
            /// copy constructor
            /// </summary>
            /// <param name="p"></param>
            public pointer_t(pointer_t p)
            {
                ptr = p.ptr;
                count = p.count;
            }

            /// <summary>
            /// constructor that allows caller to specify ptr and count
            /// </summary>
            /// <param name="node"></param>
            /// <param name="c"></param>
            public pointer_t(node_t node, long c)
            {
                ptr = node;
                count = c;
            }
        }

        private pointer_t Head;
        private pointer_t Tail;

        public MSQueue()
        {
            var node = new node_t();
            Head.ptr = Tail.ptr = node;
        }

        /// <summary>
        /// CAS
        /// stands for Compare And Swap
        /// Interlocked Compare and Exchange operation
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="compared"></param>
        /// <param name="exchange"></param>
        /// <returns></returns>
        private bool CAS(ref pointer_t destination, pointer_t compared, pointer_t exchange)
        {
            if (compared.ptr == Interlocked.CompareExchange(ref destination.ptr, exchange.ptr, compared.ptr))
            {
                Interlocked.Exchange(ref destination.count, exchange.count);
                return true;
            }

            return false;
        }

        public bool deque(ref T t)
        {
            pointer_t head;

            // Keep trying until deque is done
            var bDequeNotDone = true;
            while (bDequeNotDone)
            {
                // read head
                head = Head;

                // read tail
                var tail = Tail;

                // read next
                var next = head.ptr.next;

                // Are head, tail, and next consistent?
                if (head.count == Head.count && head.ptr == Head.ptr)
                {
                    // is tail falling behind
                    if (head.ptr == tail.ptr)
                    {
                        // is the queue empty?
                        if (null == next.ptr)
                        {
                            // queue is empty cannnot dequeue
                            return false;
                        }

                        // Tail is falling behind. try to advance it
                        CAS(ref Tail, tail, new pointer_t(next.ptr, tail.count + 1));
                    } // endif

                    else // No need to deal with tail
                    {
                        // read value before CAS otherwise another deque might try to free the next node
                        t = next.ptr.value;

                        // try to swing the head to the next node
                        if (CAS(ref Head, head, new pointer_t(next.ptr, head.count + 1)))
                        {
                            bDequeNotDone = false;
                        }
                    }
                } // endif
            } // endloop

            // dispose of head.ptr
            return true;
        }

        public void enqueue(T t)
        {
            // Allocate a new node from the free list
            var node = new node_t();

            // copy enqueued value into node
            node.value = t;

            // keep trying until Enqueue is done
            var bEnqueueNotDone = true;

            while (bEnqueueNotDone)
            {
                // read Tail.ptr and Tail.count together
                var tail = Tail;

                // read next ptr and next count together
                var next = tail.ptr.next;

                // are tail and next consistent
                if (tail.count == Tail.count && tail.ptr == Tail.ptr)
                {
                    // was tail pointing to the last node?
                    if (null == next.ptr)
                    {
                        if (CAS(ref tail.ptr.next, next, new pointer_t(node, next.count + 1)))
                        {
                            bEnqueueNotDone = false;
                        } // endif
                    } // endif

                    else // tail was not pointing to last node
                    {
                        // try to swing Tail to the next node
                        CAS(ref Tail, tail, new pointer_t(next.ptr, tail.count + 1));
                    }
                } // endif
            } // endloop
        }
    }
}