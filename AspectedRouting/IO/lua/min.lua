function min(list)
    local min = nil
    for _, value in pairs(list) do
        if(value ~= nil) then
            if (min == nil) then
                min = value
            elseif (value < min) then
                min = value
            end
        end
    end

    return min;
end