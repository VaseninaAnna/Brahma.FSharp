(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"

(**
Brahma.FSharp
======================

Documentation



Brahma.FSharp is a library for F# quotations to OpenCL translation.

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The Brahma.FSharp library can be <a href="https://nuget.org/packages/Brahma.FSharp">installed from NuGet</a>:
      <pre>PM> Install-Package Brahma.FSharp</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

If you want to use Brahma.FSharp on Linux/macOS, check OpenCL.Net.dll.config after installation and fix path to opencl.dll if necessary.

Features of Brahma.FSharp:

 * We are aimed to translate native F# code to OpenCL with minimization of different wrappers and custom types.
 * We use OpenCL for communication with GPU. So, you can work not only with NVIDIA hardware but with any device, 
which supports OpenCL (e.g. with AMD devices).
 * We support tuples and structures.
 * We can use strongly typed kernels from OpenCL code in F#.

Example
-------

This example demonstrates using a function defined in this library.

*)

module MatrixMultiply

open OpenCL.Net
open Brahma.OpenCL
open Brahma.FSharp.OpenCL.Core
open Microsoft.FSharp.Quotations
open Brahma.FSharp.OpenCL.Extensions

let random = new System.Random()
        
let MakeMatrix rows cols =
    Array.init (rows * cols) (fun i -> float32 (random.NextDouble()))

let GetOutputMatrixDimensions aRows aCols bRows bCols =
    if aCols <> bRows
    then failwith "Cannot multiply these two matrices"
    aRows,bCols

let Multiply (a:array<_>) aRows aCols (b:array<_>) bRows bCols (c:array<_>) =
    let cRows, cCols = GetOutputMatrixDimensions aRows aCols bRows bCols
    for i in 0 .. cRows - 1 do
        for j in 0 .. cCols - 1 do
            let mutable buf = 0.0f
            for k in 0 .. aCols - 1 do
                 buf <- buf + a.[i * aCols + k] * b.[k * bCols + j]
            c.[i * cCols + j] <- c.[i * cCols + j] + buf
    
let Main platformName mSize =    

    let m1 = (MakeMatrix mSize mSize)
    let m2 = (MakeMatrix mSize mSize)
    let localWorkSize = 2
    let iterations = 10
    let deviceType = DeviceType.Default

    let provider =
        try  ComputeProvider.Create(platformName, deviceType)
        with 
        | ex -> failwith ex.Message

    let mutable commandQueue = new CommandQueue(provider, provider.Devices |> Seq.head)

    let aValues = m1
    let bValues = m2
    let cParallel = Array.zeroCreate(mSize * mSize)

    let command = 
        <@
            fun (r:_2D) (a:array<_>) (b:array<_>) (c:array<_>) -> 
                let tx = r.GlobalID0
                let ty = r.GlobalID1
                let mutable buf = c.[ty * mSize + tx]
                for k in 0 .. mSize - 1 do
                    buf <- buf + (a.[ty * mSize + k] * b.[k * mSize + tx])
                c.[ty * mSize + tx] <- buf
        @>

    printfn "Multiplying two %Ax%A matrices %A times using .NET..." mSize mSize iterations
    let cNormal = Array.zeroCreate (mSize * mSize)
    let cpuStart = System.DateTime.Now
    for i in 0 .. iterations - 1 do
        Multiply aValues mSize mSize bValues mSize mSize cNormal
    let cpuTime = System.DateTime.Now - cpuStart

    printfn "done."

    printfn "Multiplying two %Ax%A matrices %A times using OpenCL and selected platform/device : %A ..." mSize mSize iterations provider

    let kernel, kernelPrepare, kernelRun = provider.Compile command
    let d =(new _2D(mSize, mSize, localWorkSize, localWorkSize))
    kernelPrepare d aValues bValues cParallel
    
    let gpuStart = System.DateTime.Now
    for i in 0 .. iterations - 1 do
        commandQueue.Add(kernelRun()).Finish() |> ignore
    let gpuTime = System.DateTime.Now - gpuStart

    let _ = commandQueue.Add(cParallel.ToHost provider).Finish()
    
    printfn "Verifying results..."
    let mutable isSuccess = true
    for i in 0 .. mSize * mSize - 1 do
        if isSuccess && System.Math.Abs(float32 (cParallel.[i] - cNormal.[i])) > 0.01f
        then
            isSuccess <- false
            printfn "Expected: %A Actual: %A Error = %A" cNormal.[i] cParallel.[i] (System.Math.Abs(cParallel.[i] - cNormal.[i]))            
            
    printfn "done."

    cpuTime.TotalMilliseconds / float iterations |> printfn "Avg. time, F#: %A"
    gpuTime.TotalMilliseconds / float iterations |> printfn "Avg. time, OpenCL: %A"

    commandQueue.Dispose()
    provider.CloseAllBuffers()
    provider.Dispose()    
            
Main "NVIDIA*" 300

(**

###Note

Sometimes calculations could be interrupted buy GPU driver (OS) timeout (TDR). 
For hot fix you can set TdrLevel registry key (KeyPath : HKEY\_LOCAL\_MACHINE\System\CurrentControlSet\Control\GraphicsDrivers) value to 0. 
If this key is not exists, then you should crete it. For more details look at ["TDR Registry Keys (Windows Drivers)"][tdr].

Samples & documentation
-----------------------

 * [Tutorial](tutorial.html) contains a further explanation of this sample library.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library. This includes additional brief samples on using most of the
   functions.

 * [More examples are available here.](https://github.com/YaccConstructor/Brahma.FSharp.Examples) 
 
Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding a new public API, please also 
consider adding [samples][content] that can be turned into a documentation. You might
also want to read the [library design notes][readme] to understand how it works.

The library is available under Eclipse Public License, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [content]: https://github.com/YaccConstructor/Brahma.FSharp/tree/master/docs/content
  [gh]: https://github.com/YaccConstructor/Brahma.FSharp
  [issues]: https://github.com/YaccConstructor/Brahma.FSharp/issues
  [readme]: https://github.com/YaccConstructor/Brahma.FSharp/blob/master/README.md
  [license]: https://github.com/YaccConstructor/Brahma.FSharp/blob/master/LICENSE.txt
  [tdr]: https://msdn.microsoft.com/en-us/library/windows/hardware/ff569918(v=vs.85).aspx
*)
