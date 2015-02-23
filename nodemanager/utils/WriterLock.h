#ifndef WRITERLOCK_H
#define WRITERLOCK_H

#include <pthread.h>

class WriterLock
{
    public:
        WriterLock(pthread_rwlock_t* l) : lock(l) { pthread_rwlock_wrlock(lock); }
        ~WriterLock() { pthread_rwlock_unlock(lock); }
    protected:
    private:
        pthread_rwlock_t* lock;
};

#endif // WRITERLOCK_H
