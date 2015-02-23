#ifndef READERLOCK_H
#define READERLOCK_H

#include <pthread.h>

class ReaderLock
{
    public:
        ReaderLock(pthread_rwlock_t* l) : lock(l) { pthread_rwlock_rdlock(lock); }
        ~ReaderLock() { pthread_rwlock_unlock(lock); }
    protected:
    private:
        pthread_rwlock_t* lock;
};

#endif // READERLOCK_H
