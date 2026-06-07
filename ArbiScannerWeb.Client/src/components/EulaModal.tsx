import React from 'react';

interface EulaModalProps {
  isOpen: boolean;
  onAgree: () => void;
  onCancel: () => void;
}

const EulaModal: React.FC<EulaModalProps> = ({ isOpen, onAgree, onCancel }) => {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center backdrop-blur-sm bg-white/30 dark:bg-black/50">
      <div className="bg-white dark:bg-gray-900 rounded-2xl shadow-xl max-w-2xl w-full mx-4 max-h-[80vh] overflow-hidden flex flex-col">
        <div className="p-6 border-b border-gray-200 dark:border-gray-700">
          <h2 className="text-2xl font-bold text-gray-800 dark:text-gray-100">End User License Agreement</h2>
        </div>
        <div className="p-6 overflow-y-auto flex-1">
          <div className="prose prose-sm max-w-none text-gray-700 dark:text-gray-300 space-y-4">
            <p className="font-semibold">Please read this End User License Agreement carefully before using ArbiScanner.</p>

            <h3 className="text-lg font-semibold mt-4 text-gray-900 dark:text-gray-100">1. Acceptance of Terms</h3>
            <p>By registering and using ArbiScanner, you agree to be bound by the terms and conditions of this agreement.</p>

            <h3 className="text-lg font-semibold mt-4 text-gray-900 dark:text-gray-100">2. Use of Service</h3>
            <p>ArbiScanner provides spread trading information and analysis tools. The service is provided "as is" without warranties of any kind.</p>

            <h3 className="text-lg font-semibold mt-4 text-gray-900 dark:text-gray-100">3. Financial Disclaimer</h3>
            <p>Trading cryptocurrencies involves substantial risk. ArbiScanner does not provide financial advice. All trading decisions are your own responsibility.</p>

            <h3 className="text-lg font-semibold mt-4 text-gray-900 dark:text-gray-100">4. Data and Privacy</h3>
            <p>We collect and process your data in accordance with our Privacy Policy. Your email and account information will be securely stored.</p>

            <h3 className="text-lg font-semibold mt-4 text-gray-900 dark:text-gray-100">5. Account Responsibilities</h3>
            <p>You are responsible for maintaining the confidentiality of your account credentials and for all activities under your account.</p>

            <h3 className="text-lg font-semibold mt-4 text-gray-900 dark:text-gray-100">6. Termination</h3>
            <p>We reserve the right to terminate or suspend access to our service at any time for violations of this agreement.</p>
          </div>
        </div>
        <div className="p-6 border-t border-gray-200 dark:border-gray-700 flex justify-end gap-3">
          <button
            onClick={onCancel}
            className="px-6 py-2 rounded-lg bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 font-medium hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={onAgree}
            className="px-6 py-2 rounded-lg bg-indigo-600 text-white font-medium hover:bg-indigo-700 transition-colors"
          >
            I Agree
          </button>
        </div>
      </div>
    </div>
  );
};

export default EulaModal;
