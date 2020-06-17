--[[ 
 Legacy function to add cycle_colour
]]

function legacy_relation_preprocessor(attributes, result)
    if (attributes.route == "bicycle") then
        -- This is a cycling network, the colour is copied
        if (attributes.colour ~= nil) then
            result.attributes_to_keep.cycle_network_colour = attributes.colour
        end
    
        if (attributes.color ~= nil) then
            -- for the americans!
            result.attributes_to_keep.cycle_network_colour = attributes.color
        end
    end
end