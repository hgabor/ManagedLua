
pcall = function(f, ...)
	__internal_setuperrorhandler()
	return true, f(...)
end


coroutine.wrap = function(f)
	local cr = coroutine.create(f)
	return function(...)
		ret = {coroutine.resume(cr, ...)}
		return unpack(ret, 2)
	end
end

