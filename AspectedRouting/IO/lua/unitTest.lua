failed_tests = false
function unit_test(f, fname, index, expected, parameters, tags)
    if (f == nil) then
        print("Trying to unit test " .. fname .. " but this function is not defined")
        failed_tests = true
        return
    end
    local actual = f(tags)
    if(expected == "null" and actual == nil) then
        -- OK!
    elseif(tonumber(actual) and tonumber(expected) and math.abs(tonumber(actual) - tonumber(expected)) < 0.1) then
        -- OK!
    elseif (expected == "no" and actual == false) then
        -- OK!
    elseif (expected == actual) then
        -- OK!
    elseif (tostring(actual) ~= tostring(expected)) then
        print("[" .. fname .. "] " .. index .. " failed: expected " .. tostring(expected) .. " but got " .. tostring(actual))
        failed_tests = true
    else
        print("[" .. fname .. "] " .. index .. " failed (generic fallback): expected " .. tostring(expected) .. " but got " .. tostring(actual))
        failed_tests = true
    end
end