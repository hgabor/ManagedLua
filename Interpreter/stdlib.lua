
coroutine.wrap = function(f)
	local cr = coroutine.create(f)
	return function(...)
		ret = {coroutine.resume(cr, ...)}
		return unpack(ret, 2)
	end
end