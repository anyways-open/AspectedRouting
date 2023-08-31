 function head(ls)
   if(ls == nil) then
       return nil
   end
   for _, v in pairs(ls) do
       if(v ~= nil) then
           return v
       end
   end
   return nil
end