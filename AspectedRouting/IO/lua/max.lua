function max(list)
    local max = nil
    for _, value in pairs(list) do
        if (value ~= nil) then
            if (max == nil) then
                max = value
            elseif (max < value) then
                max = value
            end
        end
    end

    return max;
end