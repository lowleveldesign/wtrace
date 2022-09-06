module LowLevelDesign.WTrace.Summary.SystemImages

open LowLevelDesign.WTrace

let addImage state image =
    let ind = state.SystemImageBaseAddresses.BinarySearch(image.BaseAddress)
    if ind < 0 then
        state.SystemImageBaseAddresses.Insert(~~~ind, image.BaseAddress)
        state.LoadedSystemImages.Add(image.BaseAddress, image)
    else
        Logger.Processing.TraceWarning (sprintf "Problem when adding image data: 0x%x - it is already added." image.BaseAddress)

let removeImage state baseAddress =
    let ind = state.SystemImageBaseAddresses.BinarySearch(baseAddress)
    if ind >= 0 then
        state.SystemImageBaseAddresses.RemoveAt(ind)
        state.LoadedSystemImages.Remove(baseAddress) |> ignore
    else
        Logger.Processing.TraceWarning (sprintf "Problem when disposing image data: the image 0x%x could not be found." baseAddress)

let findImage state address =
    let tryFindingModule state address =
        let ind = state.SystemImageBaseAddresses.BinarySearch(address)
        if ind < 0 then
            let ind = ~~~ind
            if ind = 0 then ValueNone
            else ValueSome (ind - 1)
        else ValueSome ind

    match tryFindingModule state address with
    | ValueNone -> ValueNone
    | ValueSome ind ->
        let baseAddress = state.SystemImageBaseAddresses[ind]
        match state.LoadedSystemImages.TryGetValue(baseAddress) with
        | (false, _) ->
            Debug.Assert(false, sprintf "Missing address in the loadedImages dictionary (0x%x)" baseAddress)
            ValueNone
        | (true, image) ->
            if address - baseAddress > uint64 image.ImageSize then
                ValueNone
            else ValueSome image


