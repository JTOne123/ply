namespace Benchmarks

open BenchmarkDotNet.Attributes
open System.Threading.Tasks
open FSharp.Control.Tasks.V2 // TaskBuilder.fs
open FSharp.Control.Tasks.Builders // Ply
open FSharp.Control.Tasks.Builders.Unsafe // Ply

[<MemoryDiagnoser>]
[<SimpleJob(targetCount = 10)>]
type MicroBenchmark() =
    [<Benchmark(OperationsPerInvoke = 100000)>]
    member _.AllocFreeReturn() = 
        for _ = 0 to 100000 do
            let ret () =
                uply {
                    return! uvtask {
                        return! uply {
                            return 1                  
                        } 
                    }
                }
            ret() |> ignore

    [<Benchmark(OperationsPerInvoke = 1000)>]
    member _.SyncExceptionSuspend() = 
        for _ = 0 to 1000 do
            let ret () =
                uply {
                    return! uvtask {
                        return! uply {
                            invalidOp "Will be suspended in an awaitable"
                            return ()                                 
                        } 
                    }
                }
            ret() |> ignore

[<MemoryDiagnoser>]
[<SimpleJob(targetCount = 20)>]
type TaskBuildersBenchmark() =
    let oldTask = ContextSensitive.task
    
    let arbitraryWork(work) = CS.Benchmarks.ArbitraryWork(work)

    // Keep at 100 minimum otherwise the C# version will do an await while the F# version
    // gets IsCompleted true due to a few more calls in between running and checking
    let workFactor = 100
    let loopCount = 5

    [<Benchmark(Description = "Ply")>]
    member _.TaskBuilderOpt () =
        (task {
            do! Task.Yield()
            let! arb = Task.Run(arbitraryWork workFactor)
            let! v = vtask {
                return! ValueTask<_>(arb)
            }

            let mutable i = loopCount
            while i > 0 do 
                let! y = Task.Run(arbitraryWork workFactor).ConfigureAwait(false)
                i <- i - 1 
                return ()

            if v > 0 then return! ValueTask<_>(v) else return 0
        }).Result

    [<Benchmark(Description = "TaskBuilder.fs v2.1.0")>]
    member _.TaskBuilder () =  
        (oldTask {
            do! Task.Yield()
            let! arb = Task.Run(arbitraryWork workFactor)
            let! v = oldTask {
                return! ValueTask<_>(arb)
            }

            let mutable i = loopCount
            while i > 0 do 
                let! y = Task.Run(arbitraryWork workFactor).ConfigureAwait(false)
                i <- i - 1 
                return ()

            if v > 0 then return! ValueTask<_>(v) else return 0
        }).Result

    [<Benchmark(Description = "C# Async Await", Baseline = true)>]
    member _.CSAsyncAwait () =  
        CS.Benchmarks.CsTasks(workFactor, loopCount).Result