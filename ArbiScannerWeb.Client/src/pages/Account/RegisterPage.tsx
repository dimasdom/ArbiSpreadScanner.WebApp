import { useTranslation } from 'react-i18next';
import Link from '../../components/LocalizedLink';
import EulaModal from '../../components/EulaModal';
import { useRegisterForm } from './hooks/useRegisterForm';

export default function SignIn() {
  const { t } = useTranslation('account');
  const {
    emailRef,
    confirmEmailRef,
    passwordRef,
    confirmPasswordRef,
    errors,
    loading,
    loginError,
    showEulaModal,
    clearFieldError,
    handleSubmit,
    handleEulaAgree,
    handleEulaCancel,
  } = useRegisterForm();

  return (
      <>
      <EulaModal
        isOpen={showEulaModal}
        onAgree={handleEulaAgree}
        onCancel={handleEulaCancel}
      />
      <form onSubmit={handleSubmit} className="min-h-[60vh] flex items-center justify-center px-4 py-8">
        <div className="w-full max-w-md">
          <div className="bg-white dark:bg-gray-900 rounded-2xl shadow-md ring-1 ring-gray-50 dark:ring-gray-700 ring-inset p-6">
            <h2 className="text-2xl font-semibold text-gray-800 dark:text-gray-100 mb-4">{t('register.title')}</h2>

            <div className="space-y-4">
              <div>
                <label htmlFor="email" className="block text-sm font-medium text-gray-700 dark:text-gray-300">{t('register.emailLabel')}</label>
                <input
                  ref={emailRef}
                  type="email"
                  id="email"
                  onChange={() => clearFieldError('email')}
                  required
                  aria-invalid={!!errors.email}
                  className="mt-1 block w-full rounded-lg bg-white dark:bg-gray-800 border-none px-3 py-2 text-gray-900 dark:text-gray-100 shadow-sm ring-1 ring-gray-50 dark:ring-gray-600 ring-inset focus:outline-none focus:ring-2 focus:ring-indigo-200"
                />
                {errors.email && (
                  <p className="mt-2 text-sm text-red-700 bg-red-50 rounded-md px-3 py-1 shadow-md ring-1 ring-red-100 ring-inset">{errors.email}</p>
                )}
              </div>

              <div>
                <label htmlFor="confirmEmail" className="block text-sm font-medium text-gray-700 dark:text-gray-300">{t('register.confirmEmailLabel')}</label>
                <input
                  ref={confirmEmailRef}
                  type="email"
                  id="confirmEmail"
                  onChange={() => clearFieldError('confirmEmail')}
                  required
                  aria-invalid={!!errors.confirmEmail}
                  className="mt-1 block w-full rounded-lg bg-white dark:bg-gray-800 border-none px-3 py-2 text-gray-900 dark:text-gray-100 shadow-sm ring-1 ring-gray-50 dark:ring-gray-600 ring-inset focus:outline-none focus:ring-2 focus:ring-indigo-200"
                />
                {errors.confirmEmail && (
                  <p className="mt-2 text-sm text-red-700 bg-red-50 rounded-md px-3 py-1 shadow-md ring-1 ring-red-100 ring-inset">{errors.confirmEmail}</p>
                )}
              </div>

              <div>
                <label htmlFor="password" className="block text-sm font-medium text-gray-700 dark:text-gray-300">{t('register.passwordLabel')}</label>
                <input
                  ref={passwordRef}
                  type="password"
                  id="password"
                  onChange={() => clearFieldError('password')}
                  required
                  aria-invalid={!!errors.password}
                  className="mt-1 block w-full rounded-lg bg-white dark:bg-gray-800 border-none px-3 py-2 text-gray-900 dark:text-gray-100 shadow-sm ring-1 ring-gray-50 dark:ring-gray-600 ring-inset focus:outline-none focus:ring-2 focus:ring-indigo-200"
                />
                {errors.password && (
                  <p className="mt-2 text-sm text-red-700 bg-red-50 rounded-md px-3 py-1 shadow-md ring-1 ring-red-100 ring-inset">{errors.password}</p>
                )}
              </div>

              <div>
                <label htmlFor="confirmPassword" className="block text-sm font-medium text-gray-700 dark:text-gray-300">{t('register.confirmPasswordLabel')}</label>
                <input
                  ref={confirmPasswordRef}
                  type="password"
                  id="confirmPassword"
                  onChange={() => clearFieldError('confirmPassword')}
                  required
                  aria-invalid={!!errors.confirmPassword}
                  className="mt-1 block w-full rounded-lg bg-white dark:bg-gray-800 border-none px-3 py-2 text-gray-900 dark:text-gray-100 shadow-sm ring-1 ring-gray-50 dark:ring-gray-600 ring-inset focus:outline-none focus:ring-2 focus:ring-indigo-200"
                />
                {errors.confirmPassword && (
                  <p className="mt-2 text-sm text-red-700 bg-red-50 rounded-md px-3 py-1 shadow-md ring-1 ring-red-100 ring-inset">{errors.confirmPassword}</p>
                )}
              </div>

              <div>
                <button
                  type="submit"
                  disabled={loading}
                  className="w-full inline-flex justify-center rounded-lg bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 font-medium py-2 px-4 shadow-md hover:shadow-lg transition-shadow ring-1 ring-gray-50 dark:ring-gray-600 ring-inset disabled:opacity-60"
                >
                  {loading ? t('register.submitting') : t('register.submitButton')}
                </button>
                <div className="flex justify-between mt-3">
                  <Link to="/account/forgotpassword" className="text-sm text-indigo-600 hover:underline">{t('register.forgotPasswordLink')}</Link>
                  <Link to="/account/login" className="text-sm text-indigo-600 hover:underline">{t('register.signInLink')}</Link>
                </div>
                {(errors.server || loginError) && (
                  <p className="mt-3 text-sm text-red-700 bg-red-50 rounded-md px-3 py-2 shadow-md ring-1 ring-red-100 ring-inset">{errors.server || loginError}</p>
                )}
              </div>
            </div>
          </div>
        </div>
      </form>
      </>
  );
}
