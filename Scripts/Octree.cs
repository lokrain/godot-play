
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;

public interface ILocatable
{
    public Vector3 Location { get; }
}

public interface IRotatable
{
    public Vector3 RotationDegrees { get; }
}

public interface IScalable
{
    public Vector3 Scale { get; }
}


public partial class Octree<T> : RefCounted, IEnumerable<T> where T : Node3D
{
    private bool disposed = false;

    // Global coordinates of the center of the octree
    public Vector3 Center { get; private set; }
    // Size of the octree in each dimension
    public Vector3 Size { get; private set; }

    private const int PerformanceThreshold = 60; // FPS threshold for adjustments 
    public int BlockCapacity { get; private set; }
    public Octree<T>[] Children { get; private set; } = null;
    public List<T> Data { get; private set; } = new();
    public bool IsSubdivided { get; private set; } = false;

    private readonly object lockObject = new();
    private const int LockTimeoutMs = 100;

    public Octree(Vector3 center, Vector3 size, int blockCapacity)
    {
        Center = center;
        Size = size;
        BlockCapacity = blockCapacity;
    }
    private int GetChildIndex(Vector3 point) => (point.X <= Center.X ? 0 : 1) + (point.Y >= Center.Y ? 0 : 2) + (point.Z <= Center.Z ? 0 : 4);
    private Aabb Bounds => new(Center - Size * 0.5f, Size);

    public Action OnNodeUpdated { get; internal set; } // LOD thing

    public void AdjustThresholdBasedOnPerformanceMetrics(double currentFrameRate)
    {
        if (currentFrameRate < PerformanceThreshold)
        {
            BlockCapacity = Math.Max(4, BlockCapacity - 2); // More aggressive reduction to improve performance
        }
        else
        {
            BlockCapacity = Math.Min(20, BlockCapacity + 1); // Gradually increase if well above target
        }
    }
    public void PerformanceManagement(double currentFrameRate = default)
    {
        if (currentFrameRate == default)
            currentFrameRate = Engine.GetFramesPerSecond();

        AdjustThresholdBasedOnPerformanceMetrics(currentFrameRate);
    }
    public void Subdivide()
    {
        IsSubdivided = true;
        Vector3 halfSize = Size / 2;  // Correct sizing for child nodes

        // Initialize children with their new center positions and sizes
        Children = new Octree<T>[8] {
            new(Center + new Vector3(-halfSize.X, halfSize.Y, -halfSize.Z), halfSize, BlockCapacity),
            new(Center + new Vector3(halfSize.X, halfSize.Y, -halfSize.Z), halfSize, BlockCapacity),
            new(Center + new Vector3(-halfSize.X, halfSize.Y, halfSize.Z), halfSize, BlockCapacity),
            new(Center + new Vector3(halfSize.X, halfSize.Y, halfSize.Z), halfSize, BlockCapacity),
            new(Center + new Vector3(-halfSize.X, -halfSize.Y, -halfSize.Z), halfSize, BlockCapacity),
            new(Center + new Vector3(halfSize.X, -halfSize.Y, -halfSize.Z), halfSize, BlockCapacity),
            new(Center + new Vector3(-halfSize.X, -halfSize.Y, halfSize.Z), halfSize, BlockCapacity),
            new(Center + new Vector3(halfSize.X, -halfSize.Y, halfSize.Z), halfSize, BlockCapacity)
        };
    }
    public void Insert(Vector3 point, T data)
    {
        ExecuteWithLock(() =>
        {
            if (!IsSubdivided && Data.Count < BlockCapacity)
            {
                Data.Add(data);
            }
            else
            {
                if (!IsSubdivided)
                {
                    Subdivide();
                }

                Children[GetChildIndex(point)].Insert(point, data);
            }
        });
    }

    public void Update(List<(Vector3 oldPoint, Vector3 newPoint, T data)> movements)
    {
        ExecuteWithLock(() =>
        {
            foreach (var movement in movements)
            {
                // Remove the data from its current position
                if (!Remove(movement.oldPoint, movement.data))
                {
                    throw new InvalidOperationException("Failed to remove data from its current position.");
                }

                // If the new position is out of bounds, grow the Octree to accommodate it
                while (!Bounds.HasPoint(movement.newPoint))
                {
                    Grow(movement.newPoint);
                }

                // Insert the data at its new position
                Insert(movement.newPoint, movement.data);
            }
        });
    }

    private void Grow(Vector3 newPoint)
    {
        int direction = GetChildIndex(newPoint);
        Vector3 newCenter = Center + Size * (direction - 0.5f);

        // Create a new, larger Octree that contains the current Octree as a child
        Octree<T> grownOctree = new(newCenter, Size * 2, BlockCapacity);

        if (IsSubdivided)
        {
            // If the current Octree is subdivided, move its children to the new Octree
            for (int i = 0; i < 8; i++)
            {
                if (i == direction)
                {
                    grownOctree.Children[i] = this;
                }
                else
                {
                    grownOctree.Children[i] = Children[i];
                }
            }
        }
        else
        {
            // If the current Octree is not subdivided, move its data to the new Octree
            foreach (T data in Data)
            {
                grownOctree.Insert(data.Position, data);
            }
        }

        // Replace the current Octree with the new, grown Octree
        Center = grownOctree.Center;
        Size = grownOctree.Size;
        Children = grownOctree.Children;
        Data = grownOctree.Data;
        IsSubdivided = grownOctree.IsSubdivided;
    }
    public bool TryMerge()
    {
        bool merged = false;
        ExecuteWithLock(() =>
        {
            if (!IsSubdivided) return;

            foreach (var child in Children)
            {
                if (child.IsSubdivided || child.Data.Any())
                {
                    return;
                }
            }

            // All children are empty and not subdivided, so merge them
            Children = null;
            IsSubdivided = false;
            merged = true;
        });
        return merged;
    }

    public bool Remove(Vector3 point, T data)
    {
        bool removed = false;
        ExecuteWithLock(() =>
        {
            if (IsSubdivided)
            {
                removed = Children[GetChildIndex(point)].Remove(point, data);
                if (removed)
                {
                    TryMerge();
                }
            }
            else
            {
                removed = Data.Remove(data);
            }
        });
        return removed;
    }
    private void ExecuteWithLock(Action action)
    {
        bool lockTaken = false;
        try
        {
            Monitor.TryEnter(lockObject, TimeSpan.FromMilliseconds(LockTimeoutMs), ref lockTaken);
            if (lockTaken)
            {
                action();
            }
            else
            {
                throw new TimeoutException("Failed to acquire lock within " + LockTimeoutMs + "ms.");
            }
        }
        finally
        {
            if (lockTaken)
            {
                Monitor.Exit(lockObject);
            }
        }
    }
    // Modify Query method to add all data from a node to the results
    public void Query(Aabb bounds, List<T> results)
    {
        if (!bounds.Intersects(Bounds))
        {
            return; // If there's no intersection, no need to check this node or its children
        }

        if (IsSubdivided)
        {
            // If the node is subdivided, recursively query each child
            foreach (var child in Children)
            {
                child.Query(bounds, results);
            }
        }
        else
        {
            // If the node is a leaf and contains data, check if the center is within the bounds
            if (Data.Any() && bounds.HasPoint(Center))
            {
                results.AddRange(Data);
            }
        }
    }


    public IEnumerator<T> GetEnumerator()
    {
        // If the node is subdivided, recursively yield all data from each child.
        if (IsSubdivided)
        {
            foreach (var child in Children)
            {
                foreach (var data in child.Data)
                {
                    yield return data;
                }
            }
        }
        // If the node is a leaf and contains data, yield this data.

        foreach (var data in Data)
        {
            yield return data;
        }
    }
    // Explicit non-generic IEnumerable implementation using the generic GetEnumerator
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator(); // This simply returns the generic enumerator.
    }

    public new void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Dispose managed state (managed objects)
                if (Children != null)
                {
                    foreach (var child in Children)
                    {
                        child?.Dispose();
                    }
                }
                if (Data != null)
                {
                    foreach (var item in Data)
                    {
                        item?.Dispose();
                    }
                }
            }
            disposed = true;
        }
    }

    ~Octree()
    {
        Dispose(false);
    }
}
