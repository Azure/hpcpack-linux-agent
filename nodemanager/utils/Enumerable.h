#ifndef ENUMERABLE_H
#define ENUMERABLE_H

#include <utility>
#include <functional>

namespace hpc
{
    namespace utils
    {
        class Enumerable
        {
            public:
                template <typename S, typename T, typename E>
                static T Sum(const S& enumerable, std::function<T(const E&)> func = [] (const E& e) { return (T)e; } )
                {
                    T result = T();
                    for (const E& e : enumerable)
                    {
                        result += func(e);
                    }

                    return std::move(result);
                }

                template <typename S, typename T, typename E>
                static T Avg(const S& enumerable, std::function<T(const E&)> func = [] (const E& e) { return (T)e; } )
                {
                    T result = T();
                    for (const E& e : enumerable)
                    {
                        result += func(e);
                    }

                    return result / enumerable.size();
                }

                template <typename S, typename T, typename E>
                static T First(const S& enumerable, std::function<T(const E&)> func = [] (const E& e) { return (T)e; } )
                {
                    for (const E& e : enumerable)
                    {
                        return func(e);
                    }

                    return T();
                }

            protected:
            private:
        };
    }
}

#endif // ENUMERABLE_H
