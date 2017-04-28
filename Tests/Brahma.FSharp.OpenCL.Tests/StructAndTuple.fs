﻿module StructAndTuple

open NUnit.Framework
open System.IO
open System
open System.Reflection


open Brahma.Helpers
open OpenCL.Net
open Brahma.OpenCL
open Brahma.FSharp.OpenCL.Core
open System
open System.Reflection
open Microsoft.FSharp.Quotations
open Brahma.FSharp.OpenCL.Extensions



[<Struct>]
type a = 
        val mutable x: int 
        val mutable y: int     
        new (x1, y1) = {x = x1; y = y1}

[<Struct>]
type b = 
    val x: int 
    val mutable y: byte      
    new (x1, y1) = {x = x1; y = y1}

[<Struct>]
type c =
    val x: int 
    val y: int
    new (x1, y1) = {x = x1; y = y1} 
    new (x1) = {x = x1; y = 0}

[<Struct>]
type d =
    val x: int 
    val y: int[]
    new (x1, y1) = {x = x1; y = y1}

[<Struct>]
type e =
    val x: int 
    val y: bool
    new (x1, y1) = {x = x1; y = y1}

[<TestFixture>]
type Translator() =
    let defaultInArrayLength = 4
    let intInArr = [|0..defaultInArrayLength-1|]
    let float32Arr = Array.init defaultInArrayLength (fun i -> float32 i)
    let _1d = new _1D(defaultInArrayLength, 1)
    let _2d = new _2D(defaultInArrayLength, 1)
    let deviceType = DeviceType.Default
    let platformName = "*"

    let provider =
        try  ComputeProvider.Create(platformName, deviceType)
        with
        | ex -> failwith ex.Message
 
    let checkResult command =
        let kernel,kernelPrepareF, kernelRunF = provider.Compile command    
        let commandQueue = new CommandQueue(provider, provider.Devices |> Seq.head)            
        let check (outArray:array<'a>) (expected:array<'a>) =        
            let cq = commandQueue.Add(kernelRunF()).Finish()
            let r = Array.zeroCreate expected.Length
            let cq2 = commandQueue.Add(outArray.ToHost(provider,r)).Finish()
            commandQueue.Dispose()
            Assert.AreEqual(expected, r)
            provider.CloseAllBuffers()
        kernelPrepareF,check
    
    [<Test>]
    member this.``some structs``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) (s1:a) (s2:b)  -> 
                    buf.[0] <- s1.x
                    buf.[1] <- s2.x
            @>

        let s1 = new a(1, 1)
        let s2 = new b(2, 86uy)
        let run1,check1 = checkResult command
        run1 _1d intInArr s1 s2      
        check1 intInArr [|1;2;2;3|]

    [<Test>]
    member this.``struct int bool``() = //doesn't work
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) (s:e)  -> 
                    buf.[0] <- s.x
            @>

        let s = new e(1, true)
        let run1,check1 = checkResult command
        run1 _1d intInArr s      
        check1 intInArr [|1;1;2;3|]

    [<Test>]
    member this.``newstruct``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>)  -> 
                let s = new a(1, 2)
                buf.[0] <- s.x
            @>

        let run,check = checkResult command
        run _1d intInArr      
        check intInArr [|1;1;2;3|]

    [<Test>]
    member this.``change field``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>)  -> 
                let mutable s = new a(1, 2)
                s.x <- 6
                buf.[0] <- s.x
            @>

        let run,check = checkResult command
        run _1d intInArr      
        check intInArr [|6;1;2;3|]

    [<Test>]
    member this.``arr of structs``() = 
        let command = 
            <@ 
                fun(range:_1D) (buf:array<int>) (arr:array<a>) -> 
                    buf.[0] <- arr.[0].x
            
            @>
        let s1 = new a(2, 2)
        let s2 = new a(2, 2)
        let s3 = new a(2, 2)
        let run,check = checkResult command
        run _1d intInArr [|s1;s2;s3|]       
        check intInArr [|2;1;2;3|]

    [<Test>]
    member this.``Struct with 2 constructors``() = 
        let command = 
            <@ 
                fun(range:_1D) (buf:array<int>) (s:c) -> 
                    buf.[0] <- s.x + s.y
                    let s2 = new c(6)
                    let s3 = new c(s2.y + 6)
                    buf.[1] <- s2.x
            
            @>
        let s = new c(2, 3)
        let run,check = checkResult command
        run _1d intInArr s        
        check intInArr [|5;6;2;3|]

    [<Test>]
    member this.``constructor``() = //doesn't work
        let command = 
            <@ 
                fun(range:_1D) (buf:array<int>)  -> 
                    let z = (new c(6)).x + 4
                    buf.[1] <- z
            
            @>
        let run,check = checkResult command
        run _1d intInArr         
        check intInArr [|5;6;2;3|]

    [<Test>]
    member this.``Struct with arr``() = //doesn't work
        let command = 
            <@ 
                fun(range:_1D) (buf:array<int>) (s:d) -> 
                    buf.[0] <- s.x
            
            @>
        let s = new d(1, [|1;2;3|])
        let run,check = checkResult command
        run _1d intInArr s        
        check intInArr [|1;1;2;3|]

    [<Test>]
    member this.``some tuples``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) (k1:int*int) (k2:int64*byte) (k3:float32*int) -> 
                    let x = fst k1
                    buf.[0] <- x
                    buf.[1] <- int(fst k3)
            @>
        let run,check = checkResult command
        run _1d intInArr (10, 2) (4294967297L, 4uy) (float32(0), 9) 
        check intInArr [|10;0;2;3|]

    [<Test>]
    member this.``fst, snd and new tuple``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) (k:int*int)  -> 
                    let k2 = (3, 8)
                    let x = fst k
                    let y = snd k
                    buf.[0] <- x
                    buf.[1] <- y
                    buf.[2] <- fst k2 + snd k2
            @>
        let s = new c(2)
        let run,check = checkResult command
        run _1d intInArr (10, 20) 
        check intInArr [|10;20;11;3|]

    [<Test>]
    member this.``arr of tuples``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) (k1:int*int) (arr:array<int*int>)  -> 
                    let k2 = (5, 6)
                    arr.[0] <- k1
                    arr.[1] <- k2
                    buf.[0] <- fst (arr.[0]) + snd (arr.[1]) + snd (arr.[2])
            @>
        let run,check = checkResult command
        run _1d intInArr (1, 2) [|(1, 2); (3, 4); (5, 6)|]
        check intInArr [|13;1;2;3|]

    [<Test>]
    member this.``triple``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<int>) (k:int*int*int)  -> 
                    buf.[0] <- first k
                    buf.[1] <- second k
                    buf.[2] <- third k
            @>
        let run,check = checkResult command
        run _1d intInArr (1, 2, 3)
        check intInArr [|1;2;3;3|]

    [<Test>]
    member this.``Write buffer``() = 
        let command = 
            <@ 
                fun (range:_1D) (buf:array<a>) ->
                    buf.[0] <- buf.[1] 
                    buf.[1] <- buf.[2] 
            @>
        let kernel,kernelPrepareF, kernelRunF = provider.Compile command
        let s = new a(2, 3)
        let s2 = new a(1, 2)
        let inArray = [|s;s;s2|]
        kernelPrepareF _1d inArray
        let commandQueue = new CommandQueue(provider, provider.Devices |> Seq.head)        
        let _ = commandQueue.Add(kernelRunF())
        let _ = commandQueue.Add(inArray.ToHost provider).Finish()
        let expected = [|s;s2;s2|] 
        Assert.AreEqual(expected, inArray)
        inArray.[0] <- s2
        commandQueue.Add(inArray.ToGpu provider) |> ignore
        let _ = commandQueue.Add(kernelRunF())
        let _ = commandQueue.Add(inArray.ToHost provider).Finish()
        let expected = [|s2;s2;s2|]
        Assert.AreEqual(expected, inArray)
        commandQueue.Dispose()        
        provider.CloseAllBuffers()